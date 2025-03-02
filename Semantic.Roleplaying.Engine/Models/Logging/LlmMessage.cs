namespace Semantic.Roleplaying.Engine.Models.Logging;

public class LlmMessage
{
    public string Role { get; set; }
    public string Message { get; set; }

    public LlmMessage()
    {
        Role = string.Empty;
        Message = string.Empty;
    }

    public LlmMessage(string role, string message)
    {
        Role = role;
        Message = message;
    }
}
