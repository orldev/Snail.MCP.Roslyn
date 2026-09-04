using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;

namespace Snail.MCP.Roslyn.Adapters.Workspaces;

internal sealed class RoslynReferences(IPathMap paths, SolutionCache cache) : IReferenceFinder
{
    private const string ScopeNote =
        "Scope is the loaded solution: references from projects outside it are invisible here.";

    private const string UnrestoredNote =
        "Package restore has not run for at least one project in this solution, so the compiler is missing references "
        + "and this result under-reports. Treat it as 'unknown', never as 'unused'. Run 'dotnet restore' and ask again.";

    private const string GeneratedSymbolNote =
        "This symbol is declared only in generated code, so the compiler cannot see how the original sources use it. "
        + "A Razor component is the usual case: its uses are markup tags in .razor files. "
        + "Treat an empty result as 'unknown', never as 'unused'.";

    private const string MarkupNote =
        "References with kind 'markup' live in a .razor or .cshtml file that compiles into generated code. "
        + "The file is the author's; a line number does not exist for a component tag and none is invented.";

    private const string SuppressedNote =
        "Every use found lies in generated code the caller cannot edit, so none are listed. "
        + "This is not an absence of uses.";

    private readonly SymbolResolver resolver = new(paths);

    public async Task<ReferencePage> FindAsync(
        string solutionId,
        SymbolQuery query,
        int limit,
        CancellationToken cancellationToken)
    {
        var solution = await cache.FreshAsync(solutionId, cancellationToken);
        var symbol = await resolver.ResolveAsync(solution, query, cancellationToken);

        if (symbol is null)
            return new ReferencePage(query.Id ?? string.Empty, [], 0, false, ["Symbol not found. Use search_symbols first."]);

        var cap = Math.Clamp(limit, 1, 500);
        var markup = await MarkupMap.BuildAsync(solution, cancellationToken);
        var found = await SymbolFinder.FindReferencesAsync(symbol, solution, cancellationToken);

        List<SymbolReference> collected = new(cap);
        HashSet<string> markupSeen = [];
        var total = 0;
        var suppressed = 0;

        foreach (var referenced in found)
        {
            foreach (var definition in referenced.Definition.Locations.Where(location => location.IsInSource))
                Collect(definition, referenced.Definition, "definition", markup, markupSeen, collected, cap, ref total, ref suppressed);

            foreach (var location in referenced.Locations.Where(location => !location.IsImplicit))
                Collect(location.Location, referenced.Definition, null, markup, markupSeen, collected, cap, ref total, ref suppressed);
        }

        return new ReferencePage(
            SymbolFacts.IdOf(symbol),
            collected,
            total,
            total > collected.Count,
            Notes(solution, symbol, collected, total, suppressed));
    }

    private void Collect(
        Location location,
        ISymbol symbol,
        string? fixedKind,
        IReadOnlyDictionary<string, string> markup,
        HashSet<string> markupSeen,
        List<SymbolReference> collected,
        int cap,
        ref int total,
        ref int suppressed)
    {
        var span = location.GetMappedLineSpan();

        if (string.IsNullOrEmpty(span.Path) || SourcePaths.IsUnderOutput(span.Path))
        {
            CollectMarkup(location, markup, markupSeen, collected, cap, ref total, ref suppressed);

            return;
        }

        total++;

        if (collected.Count >= cap)
            return;

        collected.Add(new SymbolReference(
            paths.ToCaller(span.Path),
            span.StartLinePosition.Line + 1,
            span.StartLinePosition.Character + 1,
            fixedKind ?? Classify(location, symbol),
            Snippet(location)));
    }

    /// <summary>A use inside generator output is translated to the markup file the author wrote, or counted as suppressed.</summary>
    private void CollectMarkup(
        Location location,
        IReadOnlyDictionary<string, string> markup,
        HashSet<string> markupSeen,
        List<SymbolReference> collected,
        int cap,
        ref int total,
        ref int suppressed)
    {
        if (location.SourceTree?.FilePath is not { } generated || !markup.TryGetValue(generated, out var authored))
        {
            suppressed++;

            return;
        }

        var caller = paths.ToCaller(authored);

        if (!markupSeen.Add(caller))
            return;

        total++;

        if (collected.Count < cap)
            collected.Add(new SymbolReference(caller, 0, 0, "markup", string.Empty));
    }

    private static string Classify(Location location, ISymbol symbol)
    {
        if (location.SourceTree is not { } tree)
            return "reference";

        var node = tree.GetRoot().FindNode(location.SourceSpan, findInsideTrivia: true, getInnermostNodeForTie: true);

        return ReferenceKinds.Classify(node, symbol);
    }

    private static string Snippet(Location location)
    {
        if (location.SourceTree is not { } tree)
            return string.Empty;

        var text = tree.GetText();
        var line = text.Lines.GetLineFromPosition(location.SourceSpan.Start);

        return line.ToString().Trim();
    }

    private static IReadOnlyList<string> Notes(
        Solution solution,
        ISymbol symbol,
        List<SymbolReference> collected,
        int total,
        int suppressed)
    {
        List<string> notes = [ScopeNote];

        if (ProjectRestore.Missing(solution).Count > 0)
            notes.Insert(0, UnrestoredNote);

        if (collected.Any(reference => reference.Kind == "markup"))
            notes.Insert(0, MarkupNote);

        if (SymbolFacts.IsGenerated(symbol, symbol.Locations.FirstOrDefault()?.SourceTree?.FilePath))
            notes.Insert(0, GeneratedSymbolNote);

        if (total == 0 && suppressed > 0)
            notes.Insert(0, SuppressedNote);

        return notes;
    }
}
