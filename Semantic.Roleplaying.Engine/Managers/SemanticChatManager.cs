using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Memory;
using Semantic.Roleplaying.Engine.Managers;
using Semantic.Roleplaying.Engine.Models;
using System.Text.Json;

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

    public async Task SaveMessage(ChatMessageContent message, int sequenceNumber, bool isInstruction)
    {
        // Don't store system messages that contain context
        if (message.Role == AuthorRole.System &&
            (message.Content?.Contains("Previous relevant context") == true ||
             message.Content?.Contains("Relevant context") == true ||
             message.Content?.Contains("Conversation Summary") == true))
        {
            return;
        }

        var metadata = ChatMessageMetadata.FromChatMessage(message, sequenceNumber, isInstruction);
        var id = GetMessageId(sequenceNumber);

        await _memory.SaveInformationAsync(
            collection: _collectionName,
            text: message.Content ?? string.Empty,
            id: id,
            description: $"Chat message from {metadata.Role}",
            additionalMetadata: JsonSerializer.Serialize(metadata));
    }

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
        catch (Exception)
        {
            return [];
        }
    }

    public async Task<IReadOnlyList<string>> SearchSimilarMessages(string query, int limit = 5)
    {
        var memories = new List<(MemoryQueryResult Memory, ChatMessageMetadata Metadata, double RelevanceScore)>();

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

            memories.Add((memory, metadata, memory.Relevance));
        }

        // Order by relevance and sequence number
        var orderedMemories = memories
            .OrderByDescending(m => m.RelevanceScore)
            .ThenBy(m => m.Metadata.SequenceNumber)
            .Take(limit)
            .ToList();

        return orderedMemories
            .Select(m => FormatMessageForContext(m.Memory.Metadata.Text, m.Metadata))
            .ToList();
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

    private static ChatMessageMetadata DeserializeMetadata(string metadata)
    {
        return JsonSerializer.Deserialize<ChatMessageMetadata>(metadata)
            ?? throw new InvalidOperationException("Failed to deserialize message metadata");
    }

    private static void AddMessageToHistory(ChatHistory history, ChatMessageMetadata metadata, string content)
    {
        switch (metadata.Role.ToLower())
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

    private static string FormatMessageForContext(string message, ChatMessageMetadata metadata)
    {
        // Extract character name from message if present (e.g., "[Emma] Hello" -> "Emma")
        var characterMatch = System.Text.RegularExpressions.Regex.Match(message, @"^\[([^\]]+)\]");
        var character = characterMatch.Success ? characterMatch.Groups[1].Value : metadata.Role;

        return $"[{character}] {message}";
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
        catch (Exception)
        {
            // Log or handle collection creation error
        }
    }
}
