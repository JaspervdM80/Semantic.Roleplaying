
using Microsoft.SemanticKernel.ChatCompletion;

namespace Semantic.Roleplaying.Engine.Services;

public interface IRoleplayService
{
    Task<string> GetResponseAsync(string userInput);
    Task LoadScenario();
    ChatHistory GetChatHistory();
}
