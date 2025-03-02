using Semantic.Roleplaying.Engine.Models;
using Semantic.Roleplaying.Engine.Models.Scenario;

namespace Semantic.Roleplaying.Engine.Services;

public interface IRoleplayService
{
    ScenarioContext? ScenarioContext { get; set; }
    Task<string> GetResponseAsync(string userInput);
    Task LoadScenario();
    List<BotChatMessage> GetChatHistory();
    ScenarioContext GetScenarioContext();
}
