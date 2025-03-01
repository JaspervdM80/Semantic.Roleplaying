using Microsoft.SemanticKernel;

namespace Semantic.Roleplaying.Engine.Models;

public record BotChatMessageMetadata
{
    public string Role { get; init; }
    public DateTime Timestamp { get; init; }
    public int SequenceNumber { get; init; }
    public string Content { get; init; }

    public BotChatMessageMetadata()
    {
        Role = string.Empty;
        Content = string.Empty;
    }

    public static BotChatMessageMetadata FromChatMessage(ChatMessageContent message, int sequenceNumber)
    {
        return new BotChatMessageMetadata
        {
            Role = message.Role.ToString().ToLower(),
            Content = message.InnerContent?.ToString() ?? string.Empty,
            Timestamp = DateTime.UtcNow,
            SequenceNumber = sequenceNumber
        };
    }
}
