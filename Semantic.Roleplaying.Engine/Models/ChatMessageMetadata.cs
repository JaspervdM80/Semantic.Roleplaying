using Microsoft.SemanticKernel;

namespace Semantic.Roleplaying.Engine.Models;

public record ChatMessageMetadata
{
    public string Role { get; init; }
    public DateTime Timestamp { get; init; }
    public int SequenceNumber { get; init; }
    public string Content { get; init; }
    public bool IsInstruction { get; init; }

    public ChatMessageMetadata()
    {
        Role = string.Empty;
        Content = string.Empty;
    }

    public static ChatMessageMetadata FromChatMessage(ChatMessageContent message, int sequenceNumber, bool isInstruction = false)
    {
        return new ChatMessageMetadata
        {
            Role = message.Role.ToString().ToLower(),
            Content = message.InnerContent?.ToString() ?? string.Empty,
            Timestamp = DateTime.UtcNow,
            SequenceNumber = sequenceNumber,
            IsInstruction = isInstruction
        };
    }
}
