using Microsoft.SemanticKernel;
using Semantic.Roleplaying.Engine.Models;

namespace Semantic.Roleplaying.Engine.Managers;

public interface IChatManager
{
    Task Load(string scenarioId);
    Task<List<BotChatMessage>> LoadChatHistory(int maxMessages = 20);
    Task SaveMessage(ChatMessageContent message, int sequenceNumber);
    Task<IReadOnlyList<BotChatMessage>> SearchSimilarMessages(string query, int limit = 5);
}
