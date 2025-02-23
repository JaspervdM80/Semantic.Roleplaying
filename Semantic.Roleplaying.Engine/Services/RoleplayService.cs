using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Semantic.Roleplaying.Engine.Managers;
using Semantic.Roleplaying.Engine.Models.Scenario;
using Semantic.Roleplaying.Engine.Prompts;
using Semantic.Roleplaying.Engine.Enums;

namespace Semantic.Roleplaying.Engine.Services;

public class RoleplayService : IRoleplayService
{
    private readonly Kernel _kernel;
    private ScenarioContext? _scenario;
    private ChatHistory _chatHistory;
    private readonly ILogger<RoleplayService> _logger;
    private readonly IChatManager _chatManager;
    private int _messageCounter;

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
                _chatHistory.AddUserMessage($"[Jasper] {userInput}");

                if (!isDescriptionCommand)
                {
                    await _chatManager.SaveMessage(_chatHistory.Last(), _messageCounter++, false);

                    var similarMessages = await _chatManager.SearchSimilarMessages(userInput);
                    if (similarMessages.Any())
                    {
                        var contextPrompt = $"Previous relevant context:\n{string.Join("\n", similarMessages)}";
                        _chatHistory.AddSystemMessage(contextPrompt);
                        await _chatManager.SaveMessage(_chatHistory.Last(), _messageCounter++, false);
                    }

                }
            }

            var chatCompletionService = _kernel.GetRequiredService<IChatCompletionService>();
            var promptSelector = new InitialPromptSelector(_scenario!);

            promptSelector.SelectPromptBasedOnUserInput(_chatHistory, userInput);

            var settings = new OpenAIPromptExecutionSettings
            {
                Temperature = isDescriptionCommand ? 0.7f : 0.4f, // Higher temperature for descriptions
                MaxTokens = isDescriptionCommand ? 1000 : 700,    // More tokens for descriptions
                TopP = isDescriptionCommand ? 0.8f : 0.4f,        // Higher creativity for descriptions
                FrequencyPenalty = 0.8f,
                PresencePenalty = 0.8f,
                ChatSystemPrompt = isDescriptionCommand ?
                "You MUST ONLY provide descriptive content. NO dialogue or interactions allowed." : null
            };

            var response = await chatCompletionService.GetChatMessageContentsAsync(
                _chatHistory,
                settings,
                _kernel);

            var responseContent = response.FirstOrDefault()?.Content ?? string.Empty;

            if (string.IsNullOrEmpty(responseContent))
            {
                throw new Exception("Received empty response from LLM");
            }

            _chatHistory.AddAssistantMessage(responseContent);
            await _chatManager.SaveMessage(_chatHistory.Last(), _messageCounter++, false);

            return responseContent;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetResponseAsync");
            return "Error: Unable to process response. Please try again.";
        }
    }
}
