namespace Semantic.Roleplaying.Engine.Models.Logging;

public class PromptLogModel
{
    public DateTime TimeStamp { get; set; }
    public int ElapsedTime { get; set; }
    public string EndPoint { get; set; }
    public string Model { get; set; }
    public bool IsStreaming { get; set; }
    public Dictionary<string, object?> Options { get; set; }
    public List<LlmMessage> History { get; set; }
    public LlmMessage Reply { get; set; }
    public ResponseProperties ResponseProperties { get; set; }

    public PromptLogModel()
    {
        EndPoint = string.Empty;
        Model = string.Empty;
        Options = [];
        History = [];
        Reply = new();
        ResponseProperties = new();
    }
}
