using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Rename;

namespace Snail.MCP.Roslyn.Adapters.Workspaces;

internal sealed class RoslynRenamer(IPathMap paths, SolutionCache cache) : IRenamePlanner
{
    private const string AmbiguousNote =
        "The name matches more than one symbol. Pick one from candidates and call again with its id: "
        + "renaming by name would guess, and guessing is what leaves half the references behind.";

    private const string GeneratedNote =
        "This symbol is declared only in generated code, so the compiler cannot rewrite the sources that produce it. "
        + "A Razor component is the usual case: the editable truth is the .razor file, its file name, and the markup "
        + "tags that use it, none of which the C# compiler treats as references. "
        + "No edits are proposed. The occurrences under 'unhandled' are the complete textual picture: judge and apply them yourself.";

    private const string FailedNote =
        "The compiler refused this rename. No edits are proposed; the occurrences under 'unhandled' are text only.";

    private const string ComponentNote =
        "This component is declared by its markup file, which is authored text even though the compiler only sees "
        + "generated code. The edits below rename the file, every markup tag that provably names this type, and "
        + "every compiler reference whose authored position the generator's #line mapping recovers. "
        + "'unhandled' lists what remains — judge each yourself. "
        + "No compile check covers these edits: verify with get_diagnostics after applying.";

    private const string MappedMemberNote =
        "This member is declared in a markup file's @code block. Every compiler reference maps back to an authored "
        + "position through #line — attribute assignments in consumer tags included — and the edits rewrite exactly "
        + "those positions. No compile check covers markup edits: verify with get_diagnostics after applying.";

    private const string SharedNameNote =
        "More than one component in this solution bears this name, so a markup tag cannot be attributed to this "
        + "one mechanically and no tag edits are proposed.";

    private const string SkippedNote =
        "{0} mapped position(s) did not carry '{1}' where the generator's #line mapping placed them and were "
        + "left alone, so this rename is incomplete. Search those files for the name yourself before applying.";

    private const string MarkupEditsNote =
        "Edits in .razor and .cshtml files rewrite the component's tags and the positions the generator's #line "
        + "mapping recovers. The compiler's conflict check does not cover markup; verify with get_diagnostics "
        + "after applying.";

    private const string UnhandledNote =
        "Occurrences below are text the compiler does not treat as references to this symbol: "
        + "string literals, comments, container registrations by name, or unrelated symbols. "
        + "They are listed, not changed. Decide each one yourself.";

    private readonly SymbolResolver resolver = new(paths);
    private readonly SolutionEdits editor = new(paths);
    private readonly TextScanner scanner = new(paths);
    private readonly MarkupEdits markup = new(paths);

    public async Task<RenamePlan> PlanAsync(
        string solutionId,
        SymbolQuery query,
        string newName,
        RenameScope scope,
        CancellationToken cancellationToken)
    {
        var solution = await cache.FreshAsync(solutionId, cancellationToken);
        var (symbol, candidates) = await resolver.PickAsync(solution, query, cancellationToken);

        if (symbol is null)
            return Empty(query, newName, candidates, candidates.Count > 1 ? AmbiguousNote : "Symbol not found. Use search_symbols first.");

        if (SymbolFacts.IsGenerated(symbol, symbol.Locations.FirstOrDefault()?.SourceTree?.FilePath))
            return await GeneratedPlanAsync(solution, symbol, newName, scope, cancellationToken);

        var options = new SymbolRenameOptions
        {
            RenameOverloads = scope.RenameOverloads,
            RenameInStrings = scope.SearchStrings,
            RenameInComments = scope.SearchComments,
            RenameFile = scope.RenameFile
        };

        Solution renamed;

        try
        {
            renamed = await Renamer.RenameSymbolAsync(solution, symbol, options, newName, cancellationToken);
        }
        catch (Exception error)
        {
            return await TextOnlyAsync(solution, symbol, newName, $"{FailedNote} {error.Message}", cancellationToken);
        }

        var edits = WithFileRename(await editor.BetweenAsync(solution, renamed, cancellationToken), symbol, newName, scope);
        var (markupEdits, markupNotes) = await MarkupSideAsync(solution, symbol, newName, scope, markupOnly: true, cancellationToken);

        edits = [.. edits, .. markupEdits];

        var unhandled = await scanner.UnhandledAsync(solution, symbol.Name, SolutionEdits.Touched(edits), cancellationToken);
        var conflicts = await SolutionEdits.ConflictsAsync(solution, renamed, cancellationToken);

        List<string> notes = [.. markupNotes];

        if (unhandled.Count > 0)
            notes.Add(UnhandledNote);

        return new RenamePlan(
            SymbolFacts.IdOf(symbol),
            symbol.Name,
            newName,
            edits,
            unhandled,
            conflicts,
            [],
            notes);
    }

    /// <summary>A symbol the compiler only sees in generated code still renames when its authored truth is
    /// recoverable: a component through its file and tags, a member through the generator's #line mapping.</summary>
    private async Task<RenamePlan> GeneratedPlanAsync(
        Solution solution,
        ISymbol symbol,
        string newName,
        RenameScope scope,
        CancellationToken cancellationToken)
    {
        if (MarkupEdits.IsComponent(symbol)
            && await MarkupEdits.DeclaringMarkupAsync(solution, symbol, cancellationToken) is { } declaring)
        {
            if (!await MarkupEdits.UniquelyNamedAsync(solution, symbol, cancellationToken))
                return await TextOnlyAsync(solution, symbol, newName, $"{SharedNameNote} {GeneratedNote}", cancellationToken);

            return await ComponentPlanAsync(solution, symbol, newName, declaring, scope, cancellationToken);
        }

        if (DeclaredInMarkup(symbol))
            return await MappedPlanAsync(solution, symbol, newName, cancellationToken);

        return await TextOnlyAsync(solution, symbol, newName, GeneratedNote, cancellationToken);
    }

    /// <summary>The plan for a component that exists only through its markup: file rename, tag rewrites, and
    /// every reference whose authored position the #line mapping recovers.</summary>
    private async Task<RenamePlan> ComponentPlanAsync(
        Solution solution,
        ISymbol symbol,
        string newName,
        (Project Project, string Authored) declaring,
        RenameScope scope,
        CancellationToken cancellationToken)
    {
        var spans = await MarkupEdits.TagSpansAsync(solution, declaring.Project, symbol.Name, cancellationToken);
        var (mapped, _) = await MarkupEdits.MappedSpansAsync(solution, symbol, markupOnly: false, cancellationToken);

        MarkupEdits.Merge(spans, mapped);

        var (edits, skipped) = await markup.ComposeAsync(solution, spans, symbol.Name, newName, cancellationToken);

        if (scope.RenameFile)
            edits = editor.WithFileRename(edits, declaring.Authored, newName);

        var unhandled = await scanner.UnhandledAsync(solution, symbol.Name, SolutionEdits.Touched(edits), cancellationToken);

        List<string> notes = [ComponentNote, .. Skipped(skipped, symbol.Name)];

        if (unhandled.Count > 0)
            notes.Add(UnhandledNote);

        return new RenamePlan(
            SymbolFacts.IdOf(symbol),
            symbol.Name,
            newName,
            edits,
            unhandled,
            [],
            [],
            notes);
    }

    /// <summary>The plan for an @code member: edits at the authored positions of its declaration and references.</summary>
    private async Task<RenamePlan> MappedPlanAsync(
        Solution solution,
        ISymbol symbol,
        string newName,
        CancellationToken cancellationToken)
    {
        var (spans, synthesized) = await MarkupEdits.MappedSpansAsync(solution, symbol, markupOnly: false, cancellationToken);
        var (edits, skipped) = await markup.ComposeAsync(solution, spans, symbol.Name, newName, cancellationToken);
        var unhandled = await scanner.UnhandledAsync(solution, symbol.Name, SolutionEdits.Touched(edits), cancellationToken);

        List<string> notes = [MappedMemberNote, .. ConventionNotes(symbol), .. Skipped(skipped, symbol.Name)];

        if (synthesized > 0)
            notes.Add($"{synthesized} use(s) exist only in code the generator synthesizes and have no authored position to edit.");

        if (unhandled.Count > 0)
            notes.Add(UnhandledNote);

        return new RenamePlan(SymbolFacts.IdOf(symbol), symbol.Name, newName, edits, unhandled, [], [], notes);
    }

    /// <summary>Markup-side edits accompanying a compiler rename: tags of a component, and reference positions
    /// the #line mapping places inside markup files.</summary>
    private async Task<(IReadOnlyList<FileEdit> Edits, IReadOnlyList<string> Notes)> MarkupSideAsync(
        Solution solution,
        ISymbol symbol,
        string newName,
        RenameScope scope,
        bool markupOnly,
        CancellationToken cancellationToken)
    {
        var (spans, _) = await MarkupEdits.MappedSpansAsync(solution, symbol, markupOnly, cancellationToken);

        if (MarkupEdits.IsComponent(symbol)
            && await MarkupEdits.UniquelyNamedAsync(solution, symbol, cancellationToken)
            && DeclaringProject(solution, symbol) is { } declaring)
            MarkupEdits.Merge(spans, await MarkupEdits.TagSpansAsync(solution, declaring, symbol.Name, cancellationToken));

        var (edits, skipped) = await markup.ComposeAsync(solution, spans, symbol.Name, newName, cancellationToken);

        if (MarkupEdits.IsComponent(symbol)
            && scope.RenameFile
            && await MarkupEdits.DeclaringMarkupAsync(solution, symbol, cancellationToken) is { } authored)
            edits = editor.WithFileRename(edits, authored.Authored, newName);

        List<string> notes = [.. ConventionNotes(symbol), .. Skipped(skipped, symbol.Name)];

        if (edits.Count > 0)
            notes.Insert(0, MarkupEditsNote);

        return (edits, notes);
    }

    private static Project? DeclaringProject(Solution solution, ISymbol symbol) =>
        symbol.Locations
            .Where(location => location.IsInSource)
            .Select(location => solution.GetDocument(location.SourceTree)?.Project)
            .OfType<Project>()
            .FirstOrDefault();

    private static bool DeclaredInMarkup(ISymbol symbol) =>
        symbol is IPropertySymbol or IMethodSymbol or IFieldSymbol or IEventSymbol
        && symbol.Locations.Any(location => location.IsInSource
            && location.GetMappedLineSpan() is { HasMappedPath: true } span
            && MarkupMap.IsMarkup(span.Path));

    private static IReadOnlyList<string> Skipped(int count, string name) =>
        count > 0 ? [string.Format(SkippedNote, count, name)] : [];

    /// <summary>@bind pairs a parameter with its 'Changed' sibling by name alone, and only a note can say so.</summary>
    private static IReadOnlyList<string> ConventionNotes(ISymbol symbol)
    {
        if (symbol.ContainingType is not { } owner)
            return [];

        List<string> notes = [];

        if (symbol is IPropertySymbol && owner.GetMembers($"{symbol.Name}Changed").Length > 0)
            notes.Add(
                $"'@bind-{symbol.Name}' pairs '{symbol.Name}' with '{symbol.Name}Changed' by naming convention. "
                + $"Rename '{symbol.Name}Changed' as well, or every @bind on this parameter breaks.");

        if (symbol.Name.EndsWith("Changed", StringComparison.Ordinal)
            && symbol.Name.Length > "Changed".Length
            && owner.GetMembers(symbol.Name[..^"Changed".Length]).Length > 0)
            notes.Add(
                $"'{symbol.Name}' backs '@bind-{symbol.Name[..^"Changed".Length]}' by naming convention: markup never "
                + "names it directly, so no markup edit exists for it, yet the binding depends on this exact name.");

        return notes;
    }

    private async Task<RenamePlan> TextOnlyAsync(
        Solution solution,
        ISymbol symbol,
        string newName,
        string note,
        CancellationToken cancellationToken)
    {
        List<TextualMatch> unhandled = [.. scanner.Declaring(solution, symbol.Name)];
        unhandled.AddRange(await scanner.UnhandledAsync(solution, symbol.Name, [], cancellationToken));

        return new RenamePlan(SymbolFacts.IdOf(symbol), symbol.Name, newName, [], unhandled, [], [], [note]);
    }

    private IReadOnlyList<FileEdit> WithFileRename(
        IReadOnlyList<FileEdit> edits,
        ISymbol symbol,
        string newName,
        RenameScope scope)
    {
        if (!scope.RenameFile || symbol is not INamedTypeSymbol)
            return edits;

        var declaring = symbol.Locations
            .Where(location => location.IsInSource)
            .Select(location => location.SourceTree?.FilePath)
            .OfType<string>()
            .FirstOrDefault(path => Path.GetFileNameWithoutExtension(path).Equals(symbol.Name, StringComparison.Ordinal));

        return declaring is null ? edits : editor.WithFileRename(edits, declaring, newName);
    }

    private static RenamePlan Empty(SymbolQuery query, string newName, IReadOnlyList<SymbolMatch> candidates, string note) =>
        new(query.Id ?? string.Empty, query.Name ?? string.Empty, newName, [], [], [], candidates, [note]);
}
