using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Semantic.Roleplaying.Engine.Configurations;
using Semantic.Roleplaying.Engine.Managers;
using Semantic.Roleplaying.Engine.Services;

namespace Semantic.Roleplaying.Engine.Extensions;

public static class ServiceCollectionExtensions
{

    public static IServiceCollection AddAIServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Bind configuration
        var aiSettings = new AIServiceSettings();
        configuration.GetSection("AIServices").Bind(aiSettings);
        services.Configure<AIServiceSettings>(configuration.GetSection("AIServices"));

        // Configure Semantic Kernel
        var kernel = Kernel.CreateBuilder()
            .AddOllamaTextEmbeddingGeneration(
                modelId: aiSettings.Models.EmbeddingModel,
                endpoint: new Uri(aiSettings.Endpoints.TextEmbedding))
            .AddOllamaChatCompletion(
                modelId: aiSettings.Models.ChatCompletionModel,
                endpoint: new Uri(aiSettings.Endpoints.LanguageModel))
            .AddQdrantVectorStore(
                host: "localhost",
                port: 6334,
                https: false)
            .Build();
        services.AddSingleton(kernel);

        // Add application services
        services.AddScoped<IChatManager, SemanticChatManager>();
        services.AddScoped<IRoleplayService, RoleplayService>();

        return services;
    }
}
