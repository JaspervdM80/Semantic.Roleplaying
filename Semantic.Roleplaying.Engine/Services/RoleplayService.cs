using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.Ollama;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Semantic.Roleplaying.Engine.Enums;
using Semantic.Roleplaying.Engine.Managers;
using Semantic.Roleplaying.Engine.Models;
using Semantic.Roleplaying.Engine.Models.Scenario;
using Semantic.Roleplaying.Engine.Prompts;

namespace Semantic.Roleplaying.Engine.Services;

public class RoleplayService : IRoleplayService
{
    private readonly Kernel _kernel;
    private ScenarioContext? _scenario;
    private List<BotChatMessage> _chatHistory;
    private readonly ILogger<RoleplayService> _logger;
    private readonly IChatManager _chatManager;
    private int _messageCounter;
    private const int MAX_WINDOW_SIZE = 10; // Keep last N messages in active window
    private const int SUMMARY_THRESHOLD = 20; // Summarize after N messages

    public RoleplayService(Kernel kernel, IChatManager chatManager, ILogger<RoleplayService> logger)
    {
        _kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _chatManager = chatManager;
        _chatHistory = [];
    }

    public async Task LoadScenario()
    {
        // Load scenario
        var scenarioJson = await File.ReadAllTextAsync("./data/sleepover_scenario.json");
        _scenario = JsonSerializer.Deserialize<ScenarioContext>(
            scenarioJson,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                WriteIndented = true
            }
        ) ?? throw new Exception("Failed to load scenario");

        await _chatManager.Load(_scenario.ModuleInfo.Id);

        await LoadChatHistoryAsync();
    }

    public ScenarioContext GetScenarioContext()
    {
        return _scenario ?? throw new InvalidOperationException("Scenario not loaded");
    }

    public List<BotChatMessage> GetChatHistory()
    {
        return _chatHistory;
    }

    private async Task LoadChatHistoryAsync()
    {
        _chatHistory = await _chatManager.LoadChatHistory();
        _messageCounter = _chatHistory.Count;
    }

    public async Task<string> GetResponseAsync(string userInput)
    {
        try
        {
            var isDescriptionCommand = InitialPromptSelector.DetermineInstructionType(userInput) == InstructionType.GeneralDescription;
            var relevantContext = new List<BotChatMessage>();

            if (!string.IsNullOrEmpty(userInput))
            {
                var input = $"[Jasper] {userInput}";

                // Add user message to history
                _chatHistory.Add(new BotChatMessage() { Content = input, Role = AuthorRole.System.Label });

                // Get relevant context from semantic memory
                if (!isDescriptionCommand)
                {
                    relevantContext = (await _chatManager.SearchSimilarMessages(userInput, 3)).ToList();
                }

                await _chatManager.SaveMessage(new ChatMessageContent(AuthorRole.System, input), _messageCounter++);
            }

            // Optimize chat history before sending to LLM
            var optimizedHistory = OptimizeChatHistory(relevantContext);

            var chatCompletionService = _kernel.GetRequiredService<IChatCompletionService>();
            var promptSelector = new InitialPromptSelector(_scenario!);

            // Apply prompt to optimized history
            promptSelector.SelectPromptBasedOnUserInput(optimizedHistory, userInput);

            var settings = new OllamaPromptExecutionSettings
            {
                Temperature = isDescriptionCommand ? 0.7f : 0.4f,
                NumPredict = isDescriptionCommand ? 1000 : 700,
                TopP = isDescriptionCommand ? 0.8f : 0.4f,
                ExtensionData = new Dictionary<string, object>
                {
                    ["repeat_penalty"] = 1.3f,
                    ["presence_penalty"] = 0.2f,
                    ["frequency_penalty"] = 0.4f,
                    ["system"] = isDescriptionCommand ? "You MUST ONLY provide descriptive content. NO dialogue or interactions allowed." : ""
                }
            };

            var response = await chatCompletionService.GetChatMessageContentsAsync(
                optimizedHistory,
                settings,
                _kernel);

            var responseContent = response.FirstOrDefault()?.Content ?? string.Empty;

            if (!string.IsNullOrEmpty(responseContent))
            {
                optimizedHistory.AddAssistantMessage(responseContent);
                await _chatManager.SaveMessage(optimizedHistory.Last(), _messageCounter++);

                // Check if we need to summarize older messages
                if (_messageCounter > SUMMARY_THRESHOLD)
                {
                    await SummarizeOldMessages();
                }
            }

            return responseContent ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetResponseAsync");
            return "Error: Unable to process response. Please try again.";
        }
    }

    private ChatHistory OptimizeChatHistory(List<BotChatMessage> relevantMessages)
    {
        var optimizedHistory = new ChatHistory();

        var recentMessages = _chatHistory
            .Where(m => m.Role != AuthorRole.System.ToString())
            .OrderBy(m => m.Moment)
            .TakeLast(MAX_WINDOW_SIZE)
            .ToList();

        recentMessages.AddRange(_chatHistory
            .Where(m => m.Role == AuthorRole.System.ToString())
            .OrderBy(m => m.Moment)
            .TakeLast((int)Math.Floor((decimal)MAX_WINDOW_SIZE / 2)));

        var sequenceNumbers = recentMessages.Select(m => m.Key);

        if (relevantMessages.Count() > 0)
        {
            recentMessages.AddRange(relevantMessages.Where(m => !sequenceNumbers.Contains(m.Key)));
        }

        foreach (var message in recentMessages.OrderBy(m => m.Moment))
        {
            switch (message.Role.ToLower())
            {
                case "user":
                    optimizedHistory.AddUserMessage(message.Content);
                    break;
                case "system":
                    optimizedHistory.AddSystemMessage(message.Content);
                    break;
                case "assistant":
                    optimizedHistory.AddAssistantMessage(message.Content);
                    break;
            }
        }

        return optimizedHistory;
    }

    private async Task SummarizeOldMessages()
    {
        try
        {
            // Get messages outside the recent window
            var oldMessages = _chatHistory
                .Where(m => !m.Role.Equals("system", StringComparison.OrdinalIgnoreCase))
                .Take(_chatHistory.Count - MAX_WINDOW_SIZE);

            if (!oldMessages.Any())
            {
                return;
            }

            var summarizationPrompt = "Summarize the following conversation while preserving key details and character interactions:\n";
            summarizationPrompt += string.Join("\n", oldMessages.Select(m => m.Content));

            // Create a new chat for summarization
            var summaryChatHistory = new ChatHistory();
            summaryChatHistory.AddSystemMessage(summarizationPrompt);

            var chatCompletionService = _kernel.GetRequiredService<IChatCompletionService>();
            var response = await chatCompletionService.GetChatMessageContentsAsync(
                summaryChatHistory,
                new OllamaPromptExecutionSettings { NumPredict = 500 },
                _kernel);

            var summary = response.FirstOrDefault()?.Content;
            if (!string.IsNullOrEmpty(summary))
            {
                var content = $"Conversation Summary: {summary}";

                // Save summary to semantic memory
                await _chatManager.SaveMessage(
                    new ChatMessageContent(AuthorRole.System, content),
                    _messageCounter++);

                // Remove old messages from active history
                _chatHistory =
                [
                    new BotChatMessage(content, AuthorRole.System),
                    // Add recent messages back
                    .. oldMessages.TakeLast(MAX_WINDOW_SIZE),
                ];
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during message summarization");
        }
    }

}
