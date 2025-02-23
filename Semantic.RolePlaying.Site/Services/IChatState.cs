
using Semantic.Roleplaying.Site.Models;

namespace Semantic.Roleplaying.Site.Services;

public interface IChatState
{
    List<ChatMessage> Messages { get; }
    event Action OnChange;
    void AddMessage(ChatMessage message);
}
