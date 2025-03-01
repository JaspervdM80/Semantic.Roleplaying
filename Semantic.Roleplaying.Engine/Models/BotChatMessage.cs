using Microsoft.SemanticKernel.ChatCompletion;

namespace Semantic.Roleplaying.Engine.Models;

public record BotChatMessage
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public double Relevance { get; init; }
    public string Content { get; init; }
    public BotChatMessageMetadata Metadata { get; init; }

    public BotChatMessage()
    {
        Relevance = 0;
        Content = string.Empty;
        Metadata = new BotChatMessageMetadata();
    }

    public BotChatMessage(string content, AuthorRole author)
    {
        Relevance = 0;
        Content = content;
        Metadata = new BotChatMessageMetadata()
        {
            Role = author.ToString()
        };
    }
}
