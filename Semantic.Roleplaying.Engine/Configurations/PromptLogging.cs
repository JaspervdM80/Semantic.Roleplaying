namespace Semantic.Roleplaying.Engine.Configurations;

public class PromptLogging
{
    public bool Enabled { get; init; }
    public string Path { get; init; }

    public PromptLogging()
    {
        Path = string.Empty;
    }
}
