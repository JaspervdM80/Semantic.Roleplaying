using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Connectors.Qdrant;
using Microsoft.SemanticKernel.Memory;
using Semantic.Roleplaying.Engine.Configurations;
using Semantic.Roleplaying.Engine.Diagnostics;
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

        var embedHttpClient = new HttpClient { BaseAddress = new Uri(aiSettings.Endpoints.TextEmbedding) };

        // Configure memory store
        var memoryStore = new QdrantMemoryStore(
            aiSettings.Endpoints.VectorDatabase,
            aiSettings.VectorSettings.EmbeddingDimensions);
        services.AddSingleton<IMemoryStore>(memoryStore);

        // Configure memory
        var memory = new MemoryBuilder()
            .WithOpenAITextEmbeddingGeneration(
                modelId: aiSettings.Models.EmbeddingModel,
                apiKey: aiSettings.Authentication.ApiKey,
                httpClient: embedHttpClient)
            .WithMemoryStore(memoryStore)
            .Build();
        services.AddSingleton(memory);

        // Configure Semantic Kernel
        var kernel = Kernel.CreateBuilder()
            .AddOllamaTextEmbeddingGeneration(
                modelId: aiSettings.Models.EmbeddingModel,
                endpoint: new Uri(aiSettings.Endpoints.TextEmbedding))
            .AddOllamaChatCompletion(
                modelId: aiSettings.Models.ChatCompletionModel,
                endpoint: new Uri(aiSettings.Endpoints.LanguageModel))
            .Build();
        services.AddSingleton(kernel);

        // Add application services
        services.AddScoped<IChatManager, SemanticChatManager>();
        services.AddScoped<IRoleplayService, RoleplayService>();
        services.AddScoped<IQdrantDiagnostics, QdrantDiagnostics>();

        return services;
    }
}
