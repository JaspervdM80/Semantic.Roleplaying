
namespace Semantic.Roleplaying.Engine.Diagnostics;

public interface IQdrantDiagnostics
{
    Task RunDiagnosticsAsync(string collectionName);
}