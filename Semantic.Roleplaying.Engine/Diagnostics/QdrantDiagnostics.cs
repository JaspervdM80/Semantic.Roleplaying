using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.Memory;

namespace Semantic.Roleplaying.Engine.Diagnostics;

public class QdrantDiagnostics : IQdrantDiagnostics
{
    private readonly ISemanticTextMemory _memory;
    private readonly ILogger<QdrantDiagnostics> _logger;
    private string _collectionName;

    public QdrantDiagnostics(ISemanticTextMemory memory, ILogger<QdrantDiagnostics> logger)
    {
        _memory = memory;
        _logger = logger;

        _collectionName = string.Empty;
    }

    public async Task RunDiagnosticsAsync(string collectionName)
    {
        _collectionName = collectionName;

        try
        {
            _logger.LogInformation("Starting Qdrant diagnostics...");

            // Test 1: Basic connectivity and collection existence
            _logger.LogInformation("Test 1: Checking collection existence");
            var searchResult = await TestBasicSearch();
            LogSearchResults("Basic Search Test", searchResult);

            // Test 2: Embedding generation
            _logger.LogInformation("Test 2: Testing embedding generation");
            await TestEmbeddingGeneration("This is a test message");

            // Test 3: Memory insertion and retrieval
            _logger.LogInformation("Test 3: Testing memory operations");
            await TestMemoryOperations();

            // Test 4: Search with different relevance scores
            _logger.LogInformation("Test 4: Testing search with different relevance thresholds");
            await TestSearchRelevanceThresholds();

            _logger.LogInformation("Diagnostics completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during diagnostics");
            throw;
        }
    }


    private async Task<IReadOnlyList<MemoryQueryResult>> TestBasicSearch()
    {
        var results = new List<MemoryQueryResult>();

        try
        {
            await foreach (var memory in _memory.SearchAsync(
                collection: _collectionName,
                query: "Test query",
                limit: 1,
                minRelevanceScore: 0.0))
            {
                results.Add(memory);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in basic search test");
            throw;
        }

        return results;
    }

    private async Task TestEmbeddingGeneration(string testText)
    {
        try
        {
            // Save a test memory to trigger embedding generation
            await _memory.SaveInformationAsync(
                collection: _collectionName,
                text: testText,
                id: $"test_{Guid.NewGuid()}",
                description: "Embedding test");

            _logger.LogInformation("Successfully generated embeddings for test text");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in embedding generation test");
            throw;
        }
    }

    private async Task TestMemoryOperations()
    {
        var testId = $"test_{Guid.NewGuid()}";
        var testText = "This is a test memory";

        try
        {
            // Save memory
            await _memory.SaveInformationAsync(
                collection: _collectionName,
                text: testText,
                id: testId,
                description: "Memory operation test");

            // Retrieve memory
            var retrievedMemory = await _memory.GetAsync(
                collection: _collectionName,
                key: testId);

            if (retrievedMemory == null)
            {
                _logger.LogError("Failed to retrieve test memory");
                return;
            }

            _logger.LogInformation("Successfully completed memory operations test");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in memory operations test");
            throw;
        }
    }

    private async Task TestSearchRelevanceThresholds()
    {
        var testQuery = "test query for relevance";
        var thresholds = new[] { 0.0, 0.3, 0.7, 0.9 };

        foreach (var threshold in thresholds)
        {
            try
            {
                var results = new List<MemoryQueryResult>();
                await foreach (var memory in _memory.SearchAsync(
                    collection: _collectionName,
                    query: testQuery,
                    limit: 5,
                    minRelevanceScore: threshold))
                {
                    results.Add(memory);
                }

                LogSearchResults($"Search with threshold {threshold}", results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in relevance threshold test (threshold: {threshold})");
            }
        }
    }

    private void LogSearchResults(string testName, IReadOnlyList<MemoryQueryResult> results)
    {
        _logger.LogInformation($"{testName} results:");
        _logger.LogInformation($"Found {results.Count} results");

        foreach (var result in results)
        {
            _logger.LogInformation($"- ID: {result.Metadata.Id}");
            _logger.LogInformation($"  Relevance: {result.Relevance}");
            _logger.LogInformation($"  Text: {result.Metadata.Text}");
        }
    }
}
