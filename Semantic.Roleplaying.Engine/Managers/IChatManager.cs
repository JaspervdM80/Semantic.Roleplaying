using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace Semantic.Roleplaying.Engine.Managers;

public interface IChatManager
{
    void Load(string scenarioId);
    Task<ChatHistory> LoadChatHistory(int maxMessages = 20);
    Task SaveMessage(ChatMessageContent message, int sequenceNumber, bool isInstruction);
    Task<IReadOnlyList<string>> SearchSimilarMessages(string query, int limit = 5);    
}
