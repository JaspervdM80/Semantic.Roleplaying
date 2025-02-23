using Semantic.Roleplaying.Site.Models;

namespace Semantic.Roleplaying.Site.Services;

public class ChatState : IChatState
{
    private readonly List<ChatMessage> _messages = new();
    public List<ChatMessage> Messages => _messages;

    public event Action? OnChange;

    public void AddMessage(ChatMessage message)
    {
        _messages.Add(message);
        OnChange?.Invoke();
    }
}

