using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;

namespace Snail.MCP.Roslyn.Adapters.Workspaces;

internal sealed class RoslynImpact(IPathMap paths, SolutionCache cache) : IImpactAnalyzer
{
    private const string ScopeNote =
        "Scope is the loaded solution: dependents in projects outside it are invisible here and are not reported as absent.";

    private const string CrossProjectNote =
        "This symbol is used from more than one project, so a change to it is a cross-project change: "
        + "compile every listed project, not just the one that declares it.";

    private const string ContractNote =
        "This member is part of an interface or an override chain. Changing its shape breaks every implementation listed, "
        + "and the compiler will not point at them until each one is compiled.";

    private const string UnhandledNote =
        "Text occurrences below are not references the compiler recognises. "
        + "Container registrations by name, configuration keys and reflection live here, and none of them break at compile time.";

    private readonly SymbolResolver _resolver = new(paths);
    private readonly TextScanner _scanner = new(paths);

    public async Task<ImpactReport> AnalyzeAsync(
        string solutionId,
        SymbolQuery query,
        int limit,
        CancellationToken cancellationToken)
    {
        var solution = await cache.FreshAsync(solutionId, cancellationToken);
        var (symbol, candidates) = await _resolver.PickAsync(solution, query, cancellationToken);

        if (symbol is null)
            return Empty(query, candidates);

        var cap = Math.Clamp(limit, 1, 200);
        var found = await SymbolFinder.FindReferencesAsync(symbol, solution, cancellationToken);
        var sites = Group(found, cap);
        var references = sites.Sum(site => site.References);

        var overrides = await SymbolFinder.FindOverridesAsync(symbol, solution, cancellationToken: cancellationToken);
        var implementations = await SymbolFinder.FindImplementationsAsync(symbol, solution, cancellationToken: cancellationToken);
        var unhandled = await _scanner.UnhandledAsync(solution, symbol.Name, [], cancellationToken);

        List<SymbolMatch> overriding = [.. overrides.Take(cap).Select(match => SymbolFacts.Match(match, paths))];
        List<SymbolMatch> implementing = [.. implementations.Take(cap).Select(match => SymbolFacts.Match(match, paths))];

        return new ImpactReport(
            SymbolFacts.IdOf(symbol),
            symbol.Name,
            symbol.Kind.ToString(),
            symbol.DeclaredAccessibility.ToString(),
            sites.Count > 1,
            references,
            sites,
            overriding,
            implementing,
            unhandled,
            Notes(sites.Count > 1, overriding.Count + implementing.Count > 0, unhandled.Count > 0));
    }

    private IReadOnlyList<ImpactSite> Group(IEnumerable<ReferencedSymbol> found, int cap)
    {
        Dictionary<string, (int Count, HashSet<string> Files)> byProject = [];

        foreach (var location in found.SelectMany(referenced => referenced.Locations).Where(location => !location.IsImplicit))
        {
            var span = location.Location.GetMappedLineSpan();

            if (string.IsNullOrEmpty(span.Path) || SourcePaths.IsUnderOutput(span.Path))
                continue;

            var project = location.Document.Project.Name;

            if (!byProject.TryGetValue(project, out var bucket))
                bucket = byProject[project] = (0, []);

            byProject[project] = (bucket.Count + 1, bucket.Files);
            bucket.Files.Add(paths.ToCaller(span.Path));
        }

        return
        [
            .. byProject
                .OrderByDescending(pair => pair.Value.Count)
                .Select(pair => new ImpactSite(pair.Key, pair.Value.Count, [.. pair.Value.Files.Order(StringComparer.Ordinal).Take(cap)]))
        ];
    }

    private static IReadOnlyList<string> Notes(bool crossProject, bool contract, bool unhandled)
    {
        List<string> notes = [ScopeNote];

        if (unhandled)
            notes.Insert(0, UnhandledNote);

        if (contract)
            notes.Insert(0, ContractNote);

        if (crossProject)
            notes.Insert(0, CrossProjectNote);

        return notes;
    }

    private static ImpactReport Empty(SymbolQuery query, IReadOnlyList<SymbolMatch> candidates) =>
        new(query.Id ?? string.Empty, query.Name ?? string.Empty, string.Empty, string.Empty, false, 0, [], [], [], [],
            candidates.Count > 1
                ? ["The name matches more than one symbol; pick one from candidates and call again by id."]
                : ["Symbol not found. Use search_symbols first."]);
}
