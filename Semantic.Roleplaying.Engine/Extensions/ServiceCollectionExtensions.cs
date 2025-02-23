using System.ClientModel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Connectors.Qdrant;
using Microsoft.SemanticKernel.Memory;
using OpenAI;
using Semantic.Roleplaying.Engine.Configurations;
using Semantic.Roleplaying.Engine.Diagnostics;
using Semantic.Roleplaying.Engine.Managers;
using Semantic.Roleplaying.Engine.Services;

namespace Semantic.Roleplaying.Engine.Extensions;

public static class ServiceCollectionExtensions
{

    public static IServiceCollection AddAIServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Bind configuration
        var aiSettings = new AIServiceSettings();
        configuration.GetSection("AIServices").Bind(aiSettings);
        services.Configure<AIServiceSettings>(configuration.GetSection("AIServices"));


        // Configure HttpClients
        var lmHttpClient = new HttpClient { BaseAddress = new Uri(aiSettings.Endpoints.LanguageModel) };
        var embedHttpClient = new HttpClient { BaseAddress = new Uri(aiSettings.Endpoints.TextEmbedding) };

        // Configure OpenAI clients
        var lmOptions = new OpenAIClientOptions { Endpoint = new Uri(aiSettings.Endpoints.LanguageModel) };
        var embedOptions = new OpenAIClientOptions { Endpoint = new Uri(aiSettings.Endpoints.TextEmbedding) };
        var apiKeyCredential = new ApiKeyCredential(aiSettings.Authentication.ApiKey);

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
            .AddOpenAITextEmbeddingGeneration(
                modelId: aiSettings.Models.EmbeddingModel,
                openAIClient: new OpenAIClient(apiKeyCredential, embedOptions),
                dimensions: aiSettings.VectorSettings.EmbeddingDimensions)
            .AddOpenAIChatCompletion(
                modelId: aiSettings.Models.ChatCompletionModel,
                openAIClient: new OpenAIClient(apiKeyCredential, lmOptions))
            .Build();
        services.AddSingleton(kernel);

        // Add application services
        services.AddScoped<IChatManager, SemanticChatManager>();
        services.AddScoped<IRoleplayService, RoleplayService>();
        services.AddScoped<IQdrantDiagnostics, QdrantDiagnostics>();

        return services;
    }

}
