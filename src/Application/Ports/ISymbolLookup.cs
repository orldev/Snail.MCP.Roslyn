namespace Snail.MCP.Roslyn.Application.Ports;

/// <summary>Turns a name or a position into a symbol the other tools can address.</summary>
internal interface ISymbolLookup
{
    Task<IReadOnlyList<SymbolMatch>> SearchAsync(
        string solutionId,
        string query,
        SymbolKindFilter kind,
        int limit,
        CancellationToken cancellationToken);

    Task<SymbolDetails?> DescribeAsync(string solutionId, SymbolQuery query, CancellationToken cancellationToken);

    Task<SymbolSource?> ReadAsync(string solutionId, SymbolQuery query, CancellationToken cancellationToken);
}
