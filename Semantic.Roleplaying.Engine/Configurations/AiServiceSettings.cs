namespace Semantic.Roleplaying.Engine.Configurations;

public class AIServiceSettings
{
    public AuthenticationSettings Authentication { get; init; } 
    public EndpointSettings Endpoints { get; init; } 
    public ModelSettings Models { get; init; } 
    public VectorSettings VectorSettings { get; init; }
    public PromptLogging PromptLogging { get; init; }

    public AIServiceSettings()
    {
        Authentication = new();
        Endpoints = new();
        Models = new();
        VectorSettings = new();
        PromptLogging = new();
    }
}
