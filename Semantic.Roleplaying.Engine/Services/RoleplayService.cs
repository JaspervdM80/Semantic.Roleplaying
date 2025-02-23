using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Semantic.Roleplaying.Engine.Enums;
using Semantic.Roleplaying.Engine.Managers;
using Semantic.Roleplaying.Engine.Models.Scenario;
using Semantic.Roleplaying.Engine.Prompts;

namespace Semantic.Roleplaying.Engine.Services;

public class RoleplayService : IRoleplayService
{
    private readonly Kernel _kernel;
    private ScenarioContext? _scenario;
    private ChatHistory _chatHistory;
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

        _chatManager.Load(_scenario.ModuleInfo.Id);

        await LoadChatHistoryAsync();
    }

    private async Task LoadChatHistoryAsync()
    {
        _chatHistory = await _chatManager.LoadChatHistory();
        _messageCounter = _chatHistory.Count;
    }
    public ChatHistory GetChatHistory()
    {
        return _chatHistory;
    }

    public async Task<string> GetResponseAsync(string userInput)
    {
        try
        {
            var isDescriptionCommand = InitialPromptSelector.DetermineInstructionType(userInput) == InstructionType.GeneralDescription;

            if (!string.IsNullOrEmpty(userInput))
            {
                // Add user message to history
                _chatHistory.AddUserMessage($"[Jasper] {userInput}");
                await _chatManager.SaveMessage(_chatHistory.Last(), _messageCounter++, false);

                // Get relevant context from semantic memory
                if (!isDescriptionCommand)
                {
                    var relevantContext = await GetRelevantContext(userInput);
                    if (!string.IsNullOrEmpty(relevantContext))
                    {
                        _chatHistory.AddSystemMessage(relevantContext);
                    }
                }
            }

            // Optimize chat history before sending to LLM
            var optimizedHistory = OptimizeChatHistory();

            var chatCompletionService = _kernel.GetRequiredService<IChatCompletionService>();
            var promptSelector = new InitialPromptSelector(_scenario!);

            // Apply prompt to optimized history
            promptSelector.SelectPromptBasedOnUserInput(optimizedHistory, userInput);

            var settings = new OpenAIPromptExecutionSettings
            {
                Temperature = isDescriptionCommand ? 0.7f : 0.4f,
                MaxTokens = isDescriptionCommand ? 1000 : 700,
                TopP = isDescriptionCommand ? 0.8f : 0.4f,
                FrequencyPenalty = 0.8f,
                PresencePenalty = 0.8f,
                ChatSystemPrompt = isDescriptionCommand ?
                    "You MUST ONLY provide descriptive content. NO dialogue or interactions allowed." : null
            };

            var response = await chatCompletionService.GetChatMessageContentsAsync(
                optimizedHistory,
                settings,
                _kernel);

            var responseContent = response.FirstOrDefault()?.Content ?? string.Empty;

            if (!string.IsNullOrEmpty(responseContent))
            {
                _chatHistory.AddAssistantMessage(responseContent);
                await _chatManager.SaveMessage(_chatHistory.Last(), _messageCounter++, false);

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

    private ChatHistory OptimizeChatHistory()
    {
        var optimizedHistory = new ChatHistory();

        // Always include the system prompt
        var systemPrompt = _chatHistory.FirstOrDefault(m => m.Role == AuthorRole.System);
        if (systemPrompt != null)
        {
            optimizedHistory.Add(systemPrompt);
        }

        // Add the most recent messages within the window
        var recentMessages = _chatHistory
            .Where(m => m.Role != AuthorRole.System)
            .TakeLast(MAX_WINDOW_SIZE);

        foreach (var message in recentMessages)
        {
            optimizedHistory.Add(message);
        }

        return optimizedHistory;
    }

    private async Task<string> GetRelevantContext(string userInput)
    {
        var similarMessages = await _chatManager.SearchSimilarMessages(userInput, 3);
        if (similarMessages.Any())
        {
            return $"Relevant context:\n{string.Join("\n", similarMessages)}";
        }
        return string.Empty;
    }

    private async Task SummarizeOldMessages()
    {
        try
        {
            // Get messages outside the recent window
            var oldMessages = _chatHistory
                .Where(m => m.Role != AuthorRole.System)
                .Take(_chatHistory.Count - MAX_WINDOW_SIZE);

            if (!oldMessages.Any())
                return;

            var summarizationPrompt = "Summarize the following conversation while preserving key details and character interactions:\n";
            summarizationPrompt += string.Join("\n", oldMessages.Select(m => m.Content));

            // Create a new chat for summarization
            var summaryChatHistory = new ChatHistory();
            summaryChatHistory.AddSystemMessage(summarizationPrompt);

            var chatCompletionService = _kernel.GetRequiredService<IChatCompletionService>();
            var response = await chatCompletionService.GetChatMessageContentsAsync(
                summaryChatHistory,
                new OpenAIPromptExecutionSettings { MaxTokens = 500 },
                _kernel);

            var summary = response.FirstOrDefault()?.Content;
            if (!string.IsNullOrEmpty(summary))
            {
                // Save summary to semantic memory
                await _chatManager.SaveMessage(
                    new ChatMessageContent(AuthorRole.System, $"Conversation Summary: {summary}"),
                    _messageCounter++,
                    true);

                // Remove old messages from active history
                _chatHistory = new ChatHistory();
                _chatHistory.AddSystemMessage($"Previous Conversation Summary: {summary}");

                // Add recent messages back
                foreach (var message in oldMessages.TakeLast(MAX_WINDOW_SIZE))
                {
                    _chatHistory.Add(message);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during message summarization");
        }
    }
}
