using Microsoft.SemanticKernel;
using Semantic.Roleplaying.Engine.Models;

namespace Semantic.Roleplaying.Engine.Managers;

public interface IChatManager
{
    Task DeleteMessage(Guid id, CancellationToken cancellationToken);
    Task Load(string scenarioId);
    Task<List<BotChatMessage>> LoadChatHistory(int maxMessages = 20);
    Task SaveMessage(ChatMessageContent message, int sequenceNumber);
    Task<List<BotChatMessage>> SearchSimilarMessages(string query, int limit = 5);
}
