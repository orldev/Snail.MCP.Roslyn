using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;

namespace Snail.MCP.Roslyn.Adapters.Workspaces;

internal sealed class RoslynHierarchy(IPathMap paths, SolutionCache cache) : IHierarchyExplorer
{
    private const string GeneratedNote =
        "This symbol is declared only in generated code, so an empty result means 'unknown', never 'none'.";

    private readonly SymbolResolver resolver = new(paths);

    public async Task<IReadOnlyList<SymbolMatch>> ImplementationsAsync(
        string solutionId,
        SymbolQuery query,
        int limit,
        CancellationToken cancellationToken)
    {
        var solution = await cache.FreshAsync(solutionId, cancellationToken);
        var symbol = await Resolve(solution, query, cancellationToken);

        if (symbol is null)
            return [];

        var cap = Math.Clamp(limit, 1, 200);

        if (symbol is not INamedTypeSymbol type)
            return Render(await SymbolFinder.FindImplementationsAsync(symbol, solution, cancellationToken: cancellationToken), cap);

        if (type.TypeKind == TypeKind.Interface)
        {
            List<ISymbol> found = [.. await SymbolFinder.FindImplementationsAsync(type, solution, cancellationToken: cancellationToken)];
            found.AddRange(await SymbolFinder.FindDerivedInterfacesAsync(type, solution, cancellationToken: cancellationToken));

            return Render(found, cap);
        }

        return Render(await SymbolFinder.FindDerivedClassesAsync(type, solution, cancellationToken: cancellationToken), cap);
    }

    public async Task<IReadOnlyList<SymbolMatch>> CallersAsync(
        string solutionId,
        SymbolQuery query,
        int limit,
        CancellationToken cancellationToken)
    {
        var solution = await cache.FreshAsync(solutionId, cancellationToken);
        var symbol = await Resolve(solution, query, cancellationToken);

        if (symbol is null)
            return [];

        var callers = await SymbolFinder.FindCallersAsync(symbol, solution, cancellationToken);

        return Render(callers.Select(caller => caller.CallingSymbol), Math.Clamp(limit, 1, 200));
    }

    public async Task<TypeHierarchy?> HierarchyAsync(
        string solutionId,
        SymbolQuery query,
        CancellationToken cancellationToken)
    {
        var solution = await cache.FreshAsync(solutionId, cancellationToken);
        var symbol = await Resolve(solution, query, cancellationToken);

        if (symbol is not INamedTypeSymbol type)
            return null;

        List<SymbolMatch> bases = [];

        for (var current = type.BaseType; current is not null; current = current.BaseType)
            bases.Add(SymbolFacts.Match(current, paths));

        var derived = type.TypeKind == TypeKind.Interface
            ? await SymbolFinder.FindImplementationsAsync(type, solution, cancellationToken: cancellationToken)
            : await SymbolFinder.FindDerivedClassesAsync(type, solution, cancellationToken: cancellationToken);

        List<string> notes = [];

        if (SymbolFacts.IsGenerated(type, type.Locations.FirstOrDefault()?.SourceTree?.FilePath))
            notes.Add(GeneratedNote);

        return new TypeHierarchy(
            SymbolFacts.IdOf(type),
            type.Name,
            bases,
            [.. type.AllInterfaces.Select(contract => SymbolFacts.Match(contract, paths))],
            Render(derived, 200),
            notes);
    }

    private async Task<ISymbol?> Resolve(Solution solution, SymbolQuery query, CancellationToken cancellationToken)
    {
        var (symbol, _) = await resolver.PickAsync(solution, query, cancellationToken);

        return symbol;
    }

    private IReadOnlyList<SymbolMatch> Render(IEnumerable<ISymbol> symbols, int cap) =>
    [
        .. symbols
            .Select(symbol => SymbolFacts.Match(symbol, paths))
            .DistinctBy(match => match.Id, StringComparer.Ordinal)
            .OrderBy(match => match.Name, StringComparer.Ordinal)
            .Take(cap)
    ];
}
