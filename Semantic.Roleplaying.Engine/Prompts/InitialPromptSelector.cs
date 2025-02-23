
using Semantic.Roleplaying.Engine.Models.Scenario;
using Semantic.Roleplaying.Engine.Enums;
using Microsoft.SemanticKernel.ChatCompletion;
using System.Text.RegularExpressions;
using Microsoft.SemanticKernel;

namespace Semantic.Roleplaying.Engine.Prompts;

public class InitialPromptSelector
{
    private readonly ScenarioContext _scenario;

    public InitialPromptSelector(ScenarioContext scenario)
    {
        _scenario = scenario;        
    }

    private string ResponseInstructions(InstructionType instructionType)
    {
        var chatMessgae = new ChatMessageContent();

        var defaultInstruction = $"""
            You are an AI managing multiple characters in a roleplay scenario.
            Scenario: {_scenario!.ModuleInfo.Description}
        
            Key rules:
            1. You control these characters: {string.Join(", ", _scenario.NPCs.Select(x => x.Name))}
            2. You do not control the character {_scenario.PlayerCharacter.Name}
            3. The human player controls {_scenario.PlayerCharacter.Name}
            4. Stay in character and maintain consistent personalities
            5. Always prefix character dialogue or context with their name in [brackets]
            7. Respond naturally as the appropriate character(s) would in the situation
            8. Remember the scenario constraints and background
            9. You may provide context to the situation
            """;

        var describeInstruction = $"""
            You are an AI managing multiple characters in a roleplay scenario.
            Scenario: {_scenario!.ModuleInfo.Description}

            1. Your next response should be a detailed, vivid description.
            2. You schould describe from the perspective of {_scenario.PlayerCharacter.Name}:           
            3. Provide sensory details and be as descriptive as possible.
            4. Do not provide any story progression or any dialogue, only a description and context of the scenario.
            5. Describe in detail character feelings, appearances and clothtes.
            """;

        var StoryInformation = $"""
            Character Information:
            {string.Join("\n\n", _scenario.NPCs.Select(npc =>
                $"{npc.Name}:\n{npc.BackStory}\nPersonality: {string.Join(", ", npc.PersonalityTraits.Select(t => $"{t.Key}: {t.Value}"))}"))}
            
            Background Context:
            {string.Join("\n", _scenario.StoryBackground.Select(bg => bg.Summary))}
            
            Current situation: {_scenario.StoryBackground.Last().Summary}
            """;

        return instructionType switch
        {
            InstructionType.StoryProgression => $"{defaultInstruction}{StoryInformation}",
            InstructionType.GeneralDescription => $"{describeInstruction}{StoryInformation}",
            _ => throw new InvalidOperationException($"Invalid enum {instructionType}")
        };
    }

    public void SelectPromptBasedOnUserInput(ChatHistory history, string userInput)
    {       
        var instructionType = InstructionType.StoryProgression;

        if (Regex.IsMatch(userInput, @"^[^\w\s]*describe", RegexOptions.IgnoreCase))
        {
            instructionType = InstructionType.GeneralDescription;
        }

        var chatMessage = new ChatMessageContent(AuthorRole.System, ResponseInstructions(instructionType));

        var promptMessages = history.Where(m => m.Role == AuthorRole.System && m.Content!.Contains("You are an AI managing")).ToList();

        if (promptMessages != null)
        {
            promptMessages.ForEach(m => history.Remove(m));
        }

        history.Insert(0, chatMessage);
    }
}
