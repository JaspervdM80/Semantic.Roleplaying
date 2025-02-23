namespace Semantic.Roleplaying.Engine.Models.Scenario;

public record ScenarioContext(
    ModuleInfo ModuleInfo,
    Character PlayerCharacter,
    List<Character> NPCs,
    List<StoryBackground> StoryBackground
);
