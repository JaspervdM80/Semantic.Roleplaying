using Semantic.Roleplaying.Site.Components;

namespace Semantic.Roleplaying.Site.Models;

public class ChatMessage
{
    public string Character { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public bool IsUserMessage { get; set; }
    public List<CharacterResponse> Responses { get; set; } = new();
}
