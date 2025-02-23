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

    public void Load(string scenarioId)
    {
        _collectionName = $"chat_history_{scenarioId}";
        CreateCollectionIfItDoesNotExists().Wait();
    }

    /// <summary>
    /// Saves a chat message to semantic memory.
    /// </summary>
    public async Task SaveMessage(ChatMessageContent message, int sequenceNumber, bool isInstruction)
    {
        var metadata = ChatMessageMetadata.FromChatMessage(message, sequenceNumber, isInstruction);
        var id = GetMessageId(sequenceNumber);

        await _memory.SaveInformationAsync(
            collection: _collectionName,
            text: message.Content ?? string.Empty,
            id: id,
            description: $"Chat message from {metadata.Role}",
            additionalMetadata: JsonSerializer.Serialize(metadata));
    }

    /// <summary>
    /// Loads the chat history from semantic memory.
    /// </summary>
    public async Task<ChatHistory> LoadChatHistory(int maxMessages = 20)
    {
        try
        {
            await CreateCollectionIfItDoesNotExists();

            var chatHistory = new ChatHistory();
            var memories = await GetOrderedMemories(maxMessages);

            foreach (var memory in memories)
            {
                var metadata = DeserializeMetadata(memory.Metadata.AdditionalMetadata);
                AddMessageToHistory(chatHistory, metadata, memory.Metadata.Text);
            }

            return chatHistory;
        }
        catch (Exception ex)
        {
            return [];
        }
    }

    /// <summary>
    /// Searches for semantically similar messages.
    /// </summary>
    public async Task<IReadOnlyList<string>> SearchSimilarMessages(string query, int limit = 5)
    {
        var memories = await GetMemoriesByRelevance(query, limit);
        return memories.Select(m => m.Metadata.Text).ToList();
    }

    private static string GetMessageId(int sequenceNumber) => $"msg_{sequenceNumber}";

    private async Task<IReadOnlyList<MemoryQueryResult>> GetOrderedMemories(int maxMessages)
    {
        var memories = new List<MemoryQueryResult>();

        await foreach (var memory in _memory.SearchAsync(
            collection: _collectionName,
            query: "System: Load recent messages",
            limit: maxMessages,
            minRelevanceScore: 0.0))
        {
            memories.Add(memory);
        }

        return memories
            .OrderBy(m => DeserializeMetadata(m.Metadata.AdditionalMetadata).SequenceNumber)
            .ToList();
    }

    private async Task<IReadOnlyList<MemoryQueryResult>> GetMemoriesByRelevance(string query, int limit)
    {
        var memories = new List<MemoryQueryResult>();
        await foreach (var memory in _memory.SearchAsync(
            collection: _collectionName,
            query: query,
            limit: limit,
            minRelevanceScore: 0.7))
        {
            memories.Add(memory);
        }
        return memories;
    }

    private static ChatMessageMetadata DeserializeMetadata(string metadata)
    {
        return JsonSerializer.Deserialize<ChatMessageMetadata>(metadata)
            ?? throw new InvalidOperationException("Failed to deserialize message metadata");
    }

    private static void AddMessageToHistory(ChatHistory history, ChatMessageMetadata metadata, string content)
    {
        switch (metadata.Role)
        {
            case "system":
                history.AddSystemMessage(content);
                break;
            case "assistant":
                history.AddAssistantMessage(content);
                break;
            case "user":
                history.AddUserMessage(content);
                break;
            default:
                throw new ArgumentException($"Unknown role: {metadata.Role}");
        }
    }

    private async Task CreateCollectionIfItDoesNotExists()
    {
        try
        {
            if (!(await _memoryStore.DoesCollectionExistAsync(_collectionName)))
            {
                await _memoryStore.CreateCollectionAsync(_collectionName);
            }
        }
        catch (Exception ex)
        { }
    }
}
