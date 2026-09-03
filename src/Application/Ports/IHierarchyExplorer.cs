namespace Snail.MCP.Roslyn.Application.Ports;

/// <summary>Follows inheritance and call edges the compiler knows and text search cannot see.</summary>
public interface IHierarchyExplorer
{
    Task<IReadOnlyList<SymbolMatch>> ImplementationsAsync(
        string solutionId,
        SymbolQuery query,
        int limit,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<SymbolMatch>> CallersAsync(
        string solutionId,
        SymbolQuery query,
        int limit,
        CancellationToken cancellationToken);

    Task<TypeHierarchy?> HierarchyAsync(string solutionId, SymbolQuery query, CancellationToken cancellationToken);
}
