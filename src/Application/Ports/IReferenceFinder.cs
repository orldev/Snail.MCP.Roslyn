namespace Snail.MCP.Roslyn.Application.Ports;

/// <summary>Answers what would break if a symbol were touched.</summary>
public interface IReferenceFinder
{
    Task<ReferencePage> FindAsync(
        string solutionId,
        SymbolQuery query,
        int limit,
        CancellationToken cancellationToken);
}
