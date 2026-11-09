namespace Snail.MCP.Roslyn.Application.Ports;

/// <summary>Answers what would break if a symbol were touched.</summary>
internal interface IReferenceFinder
{
    Task<ReferencePage> FindAsync(
        string solutionId,
        SymbolQuery query,
        int limit,
        CancellationToken cancellationToken);
}
