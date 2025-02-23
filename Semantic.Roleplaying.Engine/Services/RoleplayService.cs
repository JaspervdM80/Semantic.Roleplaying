using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
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

        //if (!_chatHistory.Any())
        //{
        //    await InitializeChat();
        //}

        _messageCounter = _chatHistory.Count;
    }

    //private async Task InitializeChat()
    //{
    //    var instructionPrompts = new InitialPromptSelector(_scenario!);

    //    _chatHistory.AddSystemMessage(instructionPrompts.ResponseInstructions(Enums.InstructionType.StoryProgression));
    //    await _chatManager.SaveMessage(_chatHistory.Last(), _messageCounter++, true);
    //    _chatHistory.AddSystemMessage(instructionPrompts.ResponseInstructions(Enums.InstructionType.GeneralDescription));
    //    await _chatManager.SaveMessage(_chatHistory.Last(), _messageCounter++, true);
    //}

    //private string HandleDescribeCommand(string userInput)
    //{
    //    var commandParts = Regex.Split(userInput, @"^[^a-zA-Z0-9]*describe", RegexOptions.IgnoreCase);

    //    if (commandParts.Length < 2)
    //    {
    //        return "Please provide more details about what you want to describe.";
    //    }

    //    var target = commandParts[1].Trim();

    //    var relevantCharacter = _scenario!.NPCs.FirstOrDefault(npc => npc.Name.Equals(target, StringComparison.OrdinalIgnoreCase));

    //    if (relevantCharacter != null)
    //    {
    //        var describePrompt = $"""
    //            Your next response should be a detailed, vivid description of {relevantCharacter.Name}, 
    //            from the perspective of {_scenario.PlayerCharacter.Name}.

    //            Include details about their appearance, personality, and any other relevant characteristics.
    //            Provide sensory details and be as descriptive as possible.
    //            Do not include any dialogue, only a description.
    //            """;

    //        return describePrompt;
    //    }
    //    else
    //    {
    //        var describePrompt = $"""
    //            Your next response should be a detailed, vivid description of the following, 
    //            from the perspective of {_scenario.PlayerCharacter.Name}:
    //            {userInput}

    //            Provide sensory details and be as descriptive as possible.
    //            Do not include any dialogue, only a description.
    //            """;

    //        return describePrompt;
    //    }
    //}

    public ChatHistory GetChatHistory()
    {
        return _chatHistory;
    }

    public async Task<string> GetResponseAsync(string userInput)
    {
        try
        {
            if (!string.IsNullOrEmpty(userInput))
            {
                var similarMessages = await _chatManager.SearchSimilarMessages(userInput);

                _chatHistory.AddUserMessage($"[Jasper] {userInput}");
                await _chatManager.SaveMessage(_chatHistory.Last(), _messageCounter++, false);

                if (similarMessages.Any())
                {
                    var contextPrompt = $"Previous relevant context:\n{string.Join("\n", similarMessages)}";
                    _chatHistory.AddSystemMessage(contextPrompt);
                    await _chatManager.SaveMessage(_chatHistory.Last(), _messageCounter++, false);
                }
            }

            var promptSelector = new InitialPromptSelector(_scenario!);
            promptSelector.SelectPromptBasedOnUserInput(_chatHistory, userInput);

            var chatCompletionService = _kernel.GetRequiredService<IChatCompletionService>();

            var settings = new OpenAIPromptExecutionSettings
            {
                Temperature = 0.4f,
                MaxTokens = 700,
                TopP = 0.40f,
                FrequencyPenalty = 0.8f,
                PresencePenalty = 0.8f
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
