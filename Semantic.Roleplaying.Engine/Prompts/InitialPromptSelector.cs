
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

    public void SelectPromptBasedOnUserInput(ChatHistory history, string userInput)
    {
        var instructionType = DetermineInstructionType(userInput);
        var systemPrompt = CreateSystemPrompt(instructionType);

        // Remove existing prompts of the same type
        var existingPrompts = history
            .Where(m => m.Role == AuthorRole.System &&
                        m.Content!.Contains($"[Instruction-{instructionType}]"))
            .ToList();

        foreach (var prompt in existingPrompts)
        {
            history.Remove(prompt);
        }

        // Add new prompt with type marker
        var chatMessage = new ChatMessageContent(AuthorRole.System, $"[Instruction-{instructionType}]\n{systemPrompt}");

        history.Insert(0, chatMessage);
    }

    public static InstructionType DetermineInstructionType(string userInput)
    {
        // Use more precise pattern matching
        var describePatterns = new[]
        {
            @"^\s*\*\s*describe",
            @"^\s*describe",
            @"^\s*/describe",
            @"^\s*\[describe\]"
        };

        return describePatterns.Any(pattern =>
            Regex.IsMatch(userInput, pattern, RegexOptions.IgnoreCase))
            ? InstructionType.GeneralDescription
            : InstructionType.StoryProgression;
    }

    private string CreateSystemPrompt(InstructionType type)
    {
        var basePrompt = $"""
        You are managing an realistic interactuive story with multiple characters. Generate responses based on the following information.
        Scenario: {_scenario!.ModuleInfo.Description}
        """;

        var specificInstructions = type switch
        {
            InstructionType.GeneralDescription => CreateDescriptionInstructions(),
            InstructionType.StoryProgression => CreateStoryProgressionInstructions(),
            _ => throw new ArgumentException($"Unsupported instruction type: {type}")
        };

        return $"{basePrompt}\n{specificInstructions}";
    }

    private string CreateDescriptionInstructions() => $"""
                    STRICT DESCRIPTION MODE - NO DIALOGUE ALLOWED
                    You are currently in DESCRIPTION ONLY mode. This is critically important:
    
                    1. ⚠️ ABSOLUTELY NO DIALOGUE OR CONVERSATIONS ALLOWED
                    2. ⚠️ NO CHARACTER ACTIONS OR INTERACTIONS
                    3. ⚠️ NO STORY PROGRESSION
                    4. ⚠️ NO BRACKETED CHARACTER NAMES
    
                    Instead, you must ONLY provide:
                    - Detailed physical descriptions
                    - Environmental details
                    - Sensory observations
                    - Current state descriptions
    
                    Perspective: Writing from {_scenario.PlayerCharacter.Name}'s viewpoint
    
                    Required description elements:
                    - Skin color, racce and ethnicity
                    - Visual details of people and surroundings
                    - Atmosphere and mood
                    - Current state of the scene
                    - Physical sexual characteristics
                    - Clothing and appearance
                    - Expressions and postures
    
                    Character Details to Describe:
                    {string.Join("\n\n", _scenario.NPCs.Select(npc =>
                        $"{npc.Name}:\n{npc.BackStory}\nAppearance: {npc.PersonalityTraits["appearance"]}\nClothing: {npc.PersonalityTraits["cloths"]}"))}
    
                    Remember: This is PURELY DESCRIPTIVE. If you include any dialogue or character interactions, you are violating these instructions.
                    """;

    private string CreateStoryProgressionInstructions() => $"""
                        ROLEPLAY INSTRUCTIONS:
                        1. Controlled characters: {string.Join(", ", _scenario.NPCs.Select(x => x.Name))}
                        2. Player character ({_scenario.PlayerCharacter.Name}) is controlled by human
                        3. Requirements:
                           - Start a scene description that sets the scene and describes what happens (1-2 paragraphs) if necessary.
                           - Stay in character
                           - Maintain consistent personalities
                           - Respect scenario constraints

                        ## CHARACTERS:
                        {string.Join("\n", _scenario.NPCs.Select(npc =>
                            $"{npc.Name}: {npc.BackStory}\nPersonality:\n {string.Join("\n- ", npc.PersonalityTraits.Select(t => $"{t.Key}: {t.Value}"))}"))}

                        ## BACKGROUND CONTEXT:
                        {string.Join("\n", _scenario.StoryBackground.Select(bg => $"{bg.Summary} ({string.Join(", ", bg.Context.Select(c => $"{c.Key}: {c.Value}"))})"))}

                        Based on the above information, generate a realistic, immersive response that shows how the scene based on the provided history unfolds and how each character reacts.
                        """;
}
