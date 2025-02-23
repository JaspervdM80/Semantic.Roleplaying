using System.ClientModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Connectors.Qdrant;
using Microsoft.SemanticKernel.Embeddings;
using Microsoft.SemanticKernel.Memory;
using OpenAI;
using Semantic.Roleplaying.Engine.Diagnostics;
using Semantic.Roleplaying.Engine.Managers;
using Semantic.Roleplaying.Engine.Services;

try
{
   

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
