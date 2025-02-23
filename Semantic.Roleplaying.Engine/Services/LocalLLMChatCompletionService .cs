using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace Semantic.Roleplaying.Engine.Services;

public class LocalLLMChatCompletionService : IChatCompletionService
{
    private readonly HttpClient _httpClient;
    private readonly string _endpoint;
    private readonly JsonSerializerOptions _jsonOptions;

    public LocalLLMChatCompletionService(string endpoint)
    {
        _httpClient = new HttpClient();
        _endpoint = endpoint;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };
    }

    public IReadOnlyDictionary<string, object?> Attributes => new Dictionary<string, object?>();

    public async Task<IReadOnlyList<ChatMessageContent>> GetChatMessageContentsAsync(
        ChatHistory chat,
        PromptExecutionSettings? settings = null,
        Kernel? kernel = null,
        CancellationToken cancellationToken = default)
    {
        var messages = chat.Select(m => new
        {
            role = m.Role.Label.ToLowerInvariant(),
            content = m.Content
        }).ToList();

        var request = new
        {
            messages,
            temperature = (float)(settings?.ExtensionData?["temperature"] ?? 0.7f),
            max_tokens = (int)(settings?.ExtensionData?["max_tokens"] ?? 500),
            top_p = (float)(settings?.ExtensionData?["top_p"] ?? 0.95f),
            frequency_penalty = (float)(settings?.ExtensionData?["frequency_penalty"] ?? 0.0f),
            presence_penalty = (float)(settings?.ExtensionData?["presence_penalty"] ?? 0.0f)
        };

        try
        {
            var response = await _httpClient.PostAsJsonAsync(_endpoint, request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<LocalLLMResponse>(_jsonOptions, cancellationToken);

            if (result?.Choices == null || result.Choices.Count == 0)
            {
                throw new Exception("No completion choices returned from the local LLM");
            }

            return new[]
            {
                    new ChatMessageContent(
                        AuthorRole.Assistant,
                        result.Choices[0].Message.Content ?? string.Empty)
                };
        }
        catch (Exception ex)
        {
            throw new KernelException("Failed to get chat completion from local LLM", ex);
        }
    }

    public IAsyncEnumerable<StreamingChatMessageContent> GetStreamingChatMessageContentsAsync(
        ChatHistory chat,
        PromptExecutionSettings? settings = null,
        Kernel? kernel = null,
        CancellationToken cancellationToken = default)
    {
        async IAsyncEnumerable<StreamingChatMessageContent> StreamResponse(
            [EnumeratorCancellation] CancellationToken ct)
        {
            var result = await GetChatMessageContentsAsync(chat, settings, kernel, ct);
            foreach (var message in result)
            {
                yield return new StreamingChatMessageContent(
                    message.Role,
                    message.Content);
            }
        }

        return StreamResponse(cancellationToken);
    }
}


public class LocalLLMResponse
{
    public List<LocalLLMChoice> Choices { get; set; } = new();
}

public class LocalLLMChoice
{
    public LocalLLMMessage Message { get; set; } = new();
}

public class LocalLLMMessage
{
    public string? Content { get; set; }
}
