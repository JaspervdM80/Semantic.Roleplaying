using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Semantic.Roleplaying.Engine.Configurations;
using Semantic.Roleplaying.Engine.Models.Logging;

namespace Semantic.Roleplaying.Engine.Handlers;

public class PromptLoggingHandler : DelegatingHandler
{
    private readonly string _logDirectory;
    private readonly string _serviceName;
    private readonly bool _isEnabled;

    public PromptLoggingHandler(string serviceName, AIServiceSettings options)
    {
        _serviceName = serviceName;
        _logDirectory = options.PromptLogging.Path;
        _isEnabled = options.PromptLogging.Enabled;

        if (_isEnabled && !Directory.Exists(_logDirectory))
        {
            throw new DirectoryNotFoundException($"PromptLogging is enabled but path '{_logDirectory}' does not exist");
        }
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var promptLog = new PromptLogModel()
        {
            TimeStamp = DateTime.Now,
            EndPoint = request.RequestUri?.AbsoluteUri ?? string.Empty
        };

        if (request.Content != null)
        {
            await ParseRequest(promptLog, request.Content, cancellationToken);
        }
               
        var response = await base.SendAsync(request, cancellationToken);

        if (response.Content != null)
        {
            await ParseResponse(promptLog, response.Content, cancellationToken);
        }

        await WriteLog(promptLog, cancellationToken);


        return response;
    }

    private async Task ParseRequest(PromptLogModel promptLog, HttpContent content, CancellationToken cancellationToken)
    {
        var requestBody = await content.ReadAsStringAsync(cancellationToken);

        var promptJson = JsonSerializer.Deserialize<JsonNode>(requestBody);

        if (promptJson == null)
        {
            return;
        }

        try
        {
            promptLog.Model = promptJson["model"]?.GetValue<string>() ?? string.Empty;
            promptLog.IsStreaming = promptJson["stream"]?.GetValue<bool>() ?? false;

            var options = promptJson["options"];

            if (options != null)
            {
                options.AsObject()
                       .AsEnumerable()
                       .ToList()
                       .ForEach(o => promptLog.Options.Add(o.Key, o.Value));
            }

            var messages = promptJson["messages"];

            if (messages != null)
            {
                foreach (var message in messages.AsArray())
                {
                    if (message == null)
                    {
                        continue;
                    }

                    promptLog.History.Add(new LlmMessage(message["role"]?.GetValue<string>() ?? string.Empty,
                                                          message["content"]!.GetValue<string>() ?? "!! NO NEW MESSAGE"));
                }
            }
        }
        catch (Exception ex)
        {
            //
        }
    }

    private async Task ParseResponse(PromptLogModel promptLog, HttpContent content, CancellationToken cancellationToken)
    {
        var responseBody = await content.ReadAsStringAsync(cancellationToken);

        var replyJosn = JsonSerializer.Deserialize<JsonNode>(responseBody);

        if (replyJosn == null)
        {
            return;
        }

        try
        {
            var message = replyJosn["message"];

            if (message != null)
            {
                promptLog.Reply = new LlmMessage(message["role"]?.GetValue<string>() ?? string.Empty,
                                                  message["content"]!.GetValue<string>() ?? "!! NO NEW MESSAGE");
            }

            promptLog.ResponseProperties.Done = replyJosn["done"]!.GetValue<bool>();
            promptLog.ResponseProperties.DoneReason = replyJosn["done_reason"]!.GetValue<string>();
            promptLog.ResponseProperties.TotalDuration = replyJosn["total_duration"]!.GetValue<double>();
            promptLog.ResponseProperties.LoadDuration = replyJosn["load_duration"]!.GetValue<double>();
            promptLog.ResponseProperties.PromptTokens = replyJosn["prompt_eval_count"]!.GetValue<double>();
            promptLog.ResponseProperties.PromptParseDuration = replyJosn["prompt_eval_duration"]!.GetValue<double>();
            promptLog.ResponseProperties.ReplyTokens = replyJosn["eval_count"]!.GetValue<double>();
            promptLog.ResponseProperties.ReplyDuration = replyJosn["eval_duration"]?.GetValue<double>() ?? 0;
        }
        catch (Exception ex)
        {

        }
    }

    private async Task WriteLog(PromptLogModel promptLog, CancellationToken cancellationToken)
    {
        var content = new StringBuilder("### Chat completion");

        content.AppendLine($"# Timestamp: {promptLog.TimeStamp.ToString("yyyy-MM-dd HH:mm:ss.ffff")}");
        content.AppendLine($"# Endpoint: {promptLog.EndPoint}");
        content.AppendLine($"# Model: {promptLog.Model}");
        content.AppendLine($"# Streaming: {promptLog.IsStreaming.ToString().ToLower()}");
        content.AppendLine($"\n# Options:");
        foreach (var option in promptLog.Options)
        {
            content.AppendLine($"- {option.Key}: {option.Value}");
        }
        content.AppendLine($"\n# Response properties:");
        content.AppendLine($"- Done: {promptLog.ResponseProperties.Done} ({promptLog.ResponseProperties.DoneReason})");
        content.AppendLine($"- Toral duration: {Math.Floor(promptLog.ResponseProperties.TotalDuration / 1_000_000)}ms");
        content.AppendLine($"- Model Load duration: {Math.Floor(promptLog.ResponseProperties.LoadDuration / 1_000_000)}ms");
        content.AppendLine($"- Prompt Evaluation duration: {Math.Floor(promptLog.ResponseProperties.PromptParseDuration / 1_000_000)}ms");
        content.AppendLine($"- Response Evaluation duration: {Math.Floor(promptLog.ResponseProperties.ReplyDuration / 1_000_000)}ms");
        content.AppendLine($"- Tokens sent: {promptLog.ResponseProperties.PromptTokens}");
        content.AppendLine($"- Tokens received: {promptLog.ResponseProperties.ReplyTokens}");

        content.AppendLine($"\n### Sent History:");

        foreach (var message in promptLog.History)
        {
            content.AppendLine($"\n## {message.Role.ToUpper()}");
            content.AppendLine($"{message.Message}");
        }

        var logFilePath = Path.Combine(_logDirectory, $"{_serviceName}_{promptLog.TimeStamp:yyyyMMdd_HHmmss_ffff}.log");

        content.AppendLine($"\n### Reply:");

        content.AppendLine($"\n## {promptLog.Reply.Role.ToUpper()}");
        content.AppendLine($"{promptLog.Reply.Message}");

        await File.WriteAllTextAsync(logFilePath, content.ToString(), cancellationToken);
    }
}
