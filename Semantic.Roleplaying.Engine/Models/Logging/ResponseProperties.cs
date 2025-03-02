namespace Semantic.Roleplaying.Engine.Models.Logging;

public class ResponseProperties
{
    public bool Done { get; set; }
    public string DoneReason { get; set; }
    public double TotalDuration { get; set; }
    public double LoadDuration { get; set; }
    public double PromptTokens { get; set; }
    public double PromptParseDuration { get; set; }
    public double ReplyTokens { get; set; }
    public double ReplyDuration { get; set; }

    public ResponseProperties()
    {
        DoneReason = string.Empty;
    }
}

