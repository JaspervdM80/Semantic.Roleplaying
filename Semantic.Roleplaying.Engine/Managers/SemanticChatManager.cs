using System.Text.Json;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.Qdrant;
using Microsoft.SemanticKernel.Embeddings;
using Semantic.Roleplaying.Engine.Models;

namespace Semantic.Roleplaying.Engine.Managers;

public class SemanticChatManager : IChatManager
{
    private readonly QdrantVectorStore _store;
    private readonly ITextEmbeddingGenerationService _embeddingService;
    private string _collectionName;
    private IVectorStoreRecordCollection<ulong, BotChatMessage> _collection = null!;

    public SemanticChatManager(Kernel kernel)
    {
        _embeddingService = kernel.GetRequiredService<ITextEmbeddingGenerationService>();
        _store = (kernel.GetRequiredService<IVectorStore>() as QdrantVectorStore)!;
        _collectionName = string.Empty;
    }

    public async Task Load(string scenarioId)
    {
        var memoryDefinition = new VectorStoreRecordDefinition
        {
            Properties = [
                new VectorStoreRecordKeyProperty("Key", typeof(Guid)),
                new VectorStoreRecordDataProperty("Moment", typeof(long)) { IsFilterable = true },
                new VectorStoreRecordDataProperty("Content", typeof(string)) { IsFullTextSearchable = true },
                new VectorStoreRecordVectorProperty("ContentEmbedding", typeof(ReadOnlyMemory<float>)) { Dimensions = 768 },
                new VectorStoreRecordDataProperty("Role", typeof(string)) { IsFilterable = true },
                new VectorStoreRecordDataProperty("Summary", typeof(string)) { IsFullTextSearchable = true },
            ]
        };

        _collectionName = $"chat_history_{scenarioId}";
        _collection = _store.GetCollection<ulong, BotChatMessage>(_collectionName, memoryDefinition);

        await _collection.CreateCollectionIfNotExistsAsync();
    }

    public async Task SaveMessage(ChatMessageContent message, int sequenceNumber)
    {
        // Don't store system messages that contain context
        if (message.Role == AuthorRole.System &&
            (message.Content?.Contains("Previous relevant context") == true ||
             message.Content?.Contains("Relevant context") == true ||
             message.Content?.Contains("Conversation Summary") == true))
        {
            return;
        }

        var embedding = await _embeddingService.GenerateEmbeddingAsync(message.Content!);

        await _collection.UpsertAsync(new BotChatMessage(message.Content!, message.Role, embedding));
    }

    public async Task<List<BotChatMessage>> LoadChatHistory(int maxMessages = 100)
    {
        try
        {
            await _collection.CreateCollectionIfNotExistsAsync();

            var chatHistory = await GetOrderedMemories(maxMessages);

            return chatHistory.ToList();
        }
        catch (Exception)
        {
            return [];
        }
    }

    public async Task<List<BotChatMessage>> SearchSimilarMessages(string query, int limit = 5)
    {
        var options = new VectorSearchOptions()
        {
            Top = limit * 2,
            IncludeVectors = false,
            IncludeTotalCount = false
        };

        var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(query);
        var records = await _collection.VectorizedSearchAsync(queryEmbedding, options);
        var memories = await records.Results.ToListAsync();

        // Order by relevance and sequence number
        return memories
            .OrderByDescending(m => m.Score)
            .Take(limit)
            .Select(m => m.Record)
            .OrderBy(m => m.Moment)
            .ToList();
    }

    private async Task<IReadOnlyList<BotChatMessage>> GetOrderedMemories(int maxMessages)
    {
        var options = new VectorSearchOptions()
        {
            Top = maxMessages,
            IncludeVectors = false,
            IncludeTotalCount = false
        };

        var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync("System: Load recent messages");
        var records = await _collection.VectorizedSearchAsync(queryEmbedding, options);
        var memories = await records.Results.ToListAsync();

        return memories
            .Select(m => m.Record)
            .OrderBy(m => m.Moment)
            .ToList();
    }

    //private static void AddMessageToHistory(ChatHistory history, BotChatMessage message)
    //{
    //    switch (message.Metadata.Role.ToLower())
    //    {
    //        case "system":
    //            history.AddSystemMessage(message.Content);
    //            break;
    //        case "assistant":
    //            history.AddAssistantMessage(message.Content);
    //            break;
    //        case "user":
    //            history.AddUserMessage(message.Content);
    //            break;
    //        default:
    //            throw new ArgumentException($"Unknown role: {message.Metadata.Role}");
    //    }
    //}

    //private static string FormatMessageForContext(string message, BotChatMessageMetadata metadata)
    //{
    //    // Extract character name from message if present (e.g., "[Emma] Hello" -> "Emma")
    //    var characterMatch = System.Text.RegularExpressions.Regex.Match(message, @"^\[([^\]]+)\]");
    //    var character = characterMatch.Success ? characterMatch.Groups[1].Value : metadata.Role;

    //    return $"[{character}] {message}";
    //}
}
