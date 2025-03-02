using System.ClientModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Connectors.Qdrant;
using Microsoft.SemanticKernel.Memory;
using OpenAI;
using Semantic.Roleplaying.Engine.Managers;
using Semantic.Roleplaying.Engine.Services;

try
{
    var apiKey = "no-key";
    var embeddingModel = "text-embedding-ada-002";
    var lmEndpoint = new Uri("http://localhost:1234/v1");
    var embedEndpoint = new Uri("http://localhost:5000/v1");
    var dimensions = 768;

    var httpClient = new HttpClient
    {
        BaseAddress = lmEndpoint,      
    };

    var lmOpenAIOptions = new OpenAIClientOptions { Endpoint = lmEndpoint };
    var embedOpenAIOptions = new OpenAIClientOptions { Endpoint = embedEndpoint };
    var apiKeyCredential = new ApiKeyCredential(apiKey);

    // Configure memory store with proper vector size for BGE model (768 dimensions)
    var memoryStore = new QdrantMemoryStore("http://localhost:6333", dimensions);

    // Configure memory with explicit OpenAI settings
    var memoryBuilder = new MemoryBuilder()
        .WithOpenAITextEmbeddingGeneration(
            modelId: embeddingModel,
            apiKey: apiKey,
            httpClient: new HttpClient() { BaseAddress = embedEndpoint })
        .WithMemoryStore(memoryStore);

    var memory = memoryBuilder.Build();

    // Setup Kernel with services
    var kernel = Kernel.CreateBuilder()
        .AddOpenAITextEmbeddingGeneration(
            modelId: embeddingModel,
            openAIClient: new OpenAIClient(apiKeyCredential, embedOpenAIOptions),
            dimensions: dimensions)
        .AddOpenAIChatCompletion(
            modelId: "l3-8b-stheno-v3.2-iq-imatrix",
            openAIClient: new OpenAIClient(apiKeyCredential, lmOpenAIOptions))
        .Build();

    // Configure services
    var services = new ServiceCollection();
    services.AddLogging(builder =>
    {
        builder.AddConsole();
        builder.SetMinimumLevel(LogLevel.Debug);
    });

    services.AddSingleton(kernel);
    services.AddSingleton(memory);
    services.AddSingleton<IMemoryStore>(memoryStore);
    services.AddScoped<IChatManager, SemanticChatManager>();
    services.AddScoped<IRoleplayService, RoleplayService>();

    var serviceProvider = services.BuildServiceProvider();

    // Test embedding generation
    //var embeddingService = kernel.GetRequiredService<ITextEmbeddingGenerationService>();
    //try
    //{
    //    Console.WriteLine("Testing embedding generation...");
    //    var testEmbedding = await embeddingService.GenerateEmbeddingAsync("Wat een stom gelul", kernel);
    //    Console.WriteLine($"Successfully generated embedding with {testEmbedding.Length} dimensions");
    //}
    //catch (Exception ex)
    //{
    //    Console.WriteLine($"Embedding generation failed: {ex.Message}");
    //    throw;
    //}

    //// Run diagnostics
    //var diag = serviceProvider.GetRequiredService<IQdrantDiagnostics>();
    //try
    //{
    //    Console.WriteLine("Running Qdrant diagnostics...");
    //    await diag.RunDiagnosticsAsync("chat_history_sleepover_scenario");
    //    Console.WriteLine("Diagnostics completed successfully");
    //}
    //catch (Exception ex)
    //{
    //    Console.WriteLine($"Diagnostics failed: {ex.Message}");
    //}

    // Initialize roleplay service
    var roleplayService = serviceProvider.GetRequiredService<IRoleplayService>();
    await roleplayService.LoadScenario();

    Console.WriteLine("Roleplay Chat Started - You are playing as Jasper");
    Console.WriteLine("Type 'exit' to end the chat");
    Console.WriteLine("----------------------------------------");


    if (roleplayService.GetChatHistory().Count > 0)
    {
        foreach (var message in roleplayService.GetChatHistory())
        {
            Console.WriteLine(message.Content + "\n");
        }
    }
    else
    {
        var response = await roleplayService.GetResponseAsync("");
        Console.WriteLine(response);
    }

    while (true)
    {
        Console.Write("\n[Jasper] ");
        var input = Console.ReadLine();

        if (string.IsNullOrEmpty(input) || input.ToLower() == "exit")
        {
            break;
        }

        var response = await roleplayService.GetResponseAsync(input);
        Console.WriteLine(response);
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Fatal error: {ex.Message}");
    Console.WriteLine(ex.StackTrace);
}
