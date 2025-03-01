using System.Text.Json;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Memory;
using Semantic.Roleplaying.Engine.Models;

namespace Semantic.Roleplaying.Engine.Managers;

public class SemanticChatManager : IChatManager
{
    private readonly ISemanticTextMemory _memory;
    private string _collectionName;
    private readonly IMemoryStore _memoryStore;


    public SemanticChatManager(ISemanticTextMemory memory, IMemoryStore memoryStore)
    {
        _memory = memory ?? throw new ArgumentNullException(nameof(memory));
        _memoryStore = memoryStore;
        _collectionName = string.Empty;
    }

    public async Task Load(string scenarioId)
    {
        _collectionName = $"chat_history_{scenarioId}";
        await CreateCollectionIfItDoesNotExists();
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

        var metadata = BotChatMessageMetadata.FromChatMessage(message, sequenceNumber);
        var id = GetMessageId(sequenceNumber);

        await _memory.SaveInformationAsync(
            collection: _collectionName,
            text: message.Content ?? string.Empty,
            id: id,
            description: $"Chat message from {metadata.Role}",
            additionalMetadata: JsonSerializer.Serialize(metadata));
    }

    public async Task<List<BotChatMessage>> LoadChatHistory(int maxMessages = 100)
    {
        try
        {
            await CreateCollectionIfItDoesNotExists();

            var chatHistory = await GetOrderedMemories(maxMessages);

            return chatHistory.ToList();
        }
        catch (Exception)
        {
            return [];
        }
    }

    public async Task<IReadOnlyList<BotChatMessage>> SearchSimilarMessages(string query, int limit = 5)
    {
        var memories = new List<BotChatMessage>();

        await foreach (var memory in _memory.SearchAsync(
            collection: _collectionName,
            query: query,
            limit: limit * 2, // Request more to allow for filtering
            minRelevanceScore: 0.7))
        {
            var metadata = DeserializeMetadata(memory.Metadata.AdditionalMetadata);

            // Skip system messages containing context or summaries
            if (metadata.Role == "system" &&
                (memory.Metadata.Text.Contains("Previous relevant context") ||
                 memory.Metadata.Text.Contains("Relevant context") ||
                 memory.Metadata.Text.Contains("Conversation Summary")))
            {
                continue;
            }

            memories.Add(new BotChatMessage
            {
                Content = memory.Metadata.Text,
                Relevance = memory.Relevance,
                Metadata = metadata
            });
        }

        // Order by relevance and sequence number
        return memories
            .OrderByDescending(m => m.Relevance)
            .Take(limit)
            .ToList();
    }

    private static string GetMessageId(int sequenceNumber) => $"msg_{sequenceNumber}";

    private async Task<IReadOnlyList<BotChatMessage>> GetOrderedMemories(int maxMessages)
    {
        var memories = new List<BotChatMessage>();

        await foreach (var memory in _memory.SearchAsync(
            collection: _collectionName,
            query: "System: Load recent messages",
            limit: maxMessages,
            minRelevanceScore: 0.0))
        {
            var metadata = DeserializeMetadata(memory.Metadata.AdditionalMetadata);

            memories.Add(new BotChatMessage
            {
                Content = memory.Metadata.Text,
                Relevance = memory.Relevance,
                Metadata = metadata
            });
        }

        return memories
            .OrderBy(m => m.Metadata.SequenceNumber)
            .ToList();
    }

    private static BotChatMessageMetadata DeserializeMetadata(string metadata)
    {
        return JsonSerializer.Deserialize<BotChatMessageMetadata>(metadata)
            ?? throw new InvalidOperationException("Failed to deserialize message metadata");
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

    private async Task CreateCollectionIfItDoesNotExists()
    {
        try
        {
            if (!(await _memoryStore.DoesCollectionExistAsync(_collectionName)))
            {
                await _memoryStore.CreateCollectionAsync(_collectionName);
            }
        }
        catch (Exception)
        {
            // Log or handle collection creation error
        }
    }
}
