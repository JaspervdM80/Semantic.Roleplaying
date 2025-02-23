namespace Semantic.Roleplaying.Engine.Models.Scenario;

public record Character(
    string Name,
    string BackStory,
    Dictionary<string, string> PersonalityTraits,
    string Prompt
 );
