using Microsoft.SemanticKernel.ChatCompletion;

namespace Semantic.Roleplaying.Engine.Models;

public record BotChatMessage
{
    public Guid Key { get; init; }
    public long Moment { get; init; }
    public string Role { get; init; }
    public string Content { get; init; }
    public string Summary { get; init; }
    public ReadOnlyMemory<float> ContentEmbedding { get; init; }

    public BotChatMessage()
    {
        Key = Guid.NewGuid();
        Moment = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        Content = string.Empty;
        Role = string.Empty;
        Summary = string.Empty;
    }

    public BotChatMessage(string content, AuthorRole author)
    {
        Key = Guid.NewGuid();
        Moment = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        Content = content;
        Summary = string.Empty;
        Role = author.Label;
    }

    public BotChatMessage(string content, AuthorRole author, ReadOnlyMemory<float> contentEmbedding)
    {
        Key = Guid.NewGuid();
        Moment = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        Summary = string.Empty;
        Content = content;
        ContentEmbedding = contentEmbedding;
        Role = author.Label;
    }
}
