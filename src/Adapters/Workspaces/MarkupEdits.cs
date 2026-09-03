using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Text;

namespace Snail.MCP.Roslyn.Adapters.Workspaces;

/// <summary>Plans edits inside markup files for the one claim text can prove: a component tag names its type.</summary>
/// <remarks>Everything else in markup — attribute values, qualified accesses, prose — stays unhandled on purpose:
/// without the Razor compiler those are guesses, and a guessed edit is worse than a named occurrence.</remarks>
internal sealed class MarkupEdits(IPathMap paths)
{
    private const string ComponentContract = "Microsoft.AspNetCore.Components.IComponent";

    public static bool IsComponent(ISymbol symbol) =>
        symbol is INamedTypeSymbol type
        && type.AllInterfaces.Any(contract => contract.ToDisplayString() == ComponentContract);

    /// <summary>The markup file whose generated code declares this symbol, with its project, or null.</summary>
    public static async Task<(Project Project, string Authored)?> DeclaringMarkupAsync(
        Solution solution,
        ISymbol symbol,
        CancellationToken cancellationToken)
    {
        var generated = symbol.Locations.FirstOrDefault(location => location.IsInSource)?.SourceTree?.FilePath;

        if (generated is null)
            return null;

        var map = await MarkupMap.BuildAsync(solution, cancellationToken);

        if (!map.TryGetValue(generated, out var authored))
            return null;

        var project = solution.Projects.FirstOrDefault(candidate => candidate.AdditionalDocuments.Any(document =>
            string.Equals(document.FilePath, authored, StringComparison.OrdinalIgnoreCase)));

        return project is null ? null : (project, authored);
    }

    /// <summary>True when exactly one component in the solution bears this name, so a tag attributes mechanically.</summary>
    public static async Task<bool> UniquelyNamedAsync(
        Solution solution,
        ISymbol symbol,
        CancellationToken cancellationToken)
    {
        var count = 0;

        foreach (var project in solution.Projects.Where(project => project.Language == LanguageNames.CSharp))
        {
            var compilation = await project.GetCompilationAsync(cancellationToken);

            if (compilation is null)
                continue;

            count += compilation.GetSymbolsWithName(symbol.Name, SymbolFilter.Type, cancellationToken).Count(IsComponent);
        }

        return count == 1;
    }

    /// <summary>Positions of every tag of the component in the markup files that can see its project.
    /// The Razor front-end supplies them when it can — a tag inside a comment or a string is then never
    /// touched — and a text scan fills in when it cannot.</summary>
    public static async Task<Dictionary<string, HashSet<(int Line, int Column)>>> TagSpansAsync(
        Solution solution,
        Project declaring,
        string name,
        CancellationToken cancellationToken)
    {
        var tag = new Regex($@"<(/?){Regex.Escape(name)}(?=[\s/>])", RegexOptions.None, TimeSpan.FromSeconds(1));
        var graph = solution.GetProjectDependencyGraph();

        HashSet<ProjectId> visible =
            [declaring.Id, .. graph.GetProjectsThatTransitivelyDependOnThisProject(declaring.Id)];

        Dictionary<string, HashSet<(int, int)>> spans = new(StringComparer.OrdinalIgnoreCase);

        foreach (var project in solution.Projects.Where(project => visible.Contains(project.Id)))
        {
            foreach (var document in project.AdditionalDocuments.Where(document => MarkupMap.IsMarkup(document.FilePath)))
            {
                var text = await document.GetTextAsync(cancellationToken);

                if (RazorMarkup.Parse(text.ToString(), document.FilePath!) is { } elements)
                {
                    foreach (var element in elements)
                        TagSpansOf(element, name, text, document.FilePath!, spans);

                    continue;
                }

                foreach (var line in text.Lines)
                {
                    foreach (Match match in tag.Matches(line.ToString()))
                        At(spans, document.FilePath!, line.LineNumber + 1, match.Index + 1 + match.Groups[1].Length);
                }
            }
        }

        return spans;
    }

    /// <summary>The opening and closing tag name positions of one parsed element, when it names the component —
    /// directly or as the last segment of a qualified tag.</summary>
    private static void TagSpansOf(
        RazorMarkup.Element element,
        string name,
        SourceText text,
        string path,
        Dictionary<string, HashSet<(int Line, int Column)>> spans)
    {
        if (!string.Equals(element.TagName, name, StringComparison.Ordinal)
            && !element.TagName.EndsWith($".{name}", StringComparison.Ordinal))
            return;

        var prefix = element.TagName.Length - name.Length;

        void AtIndex(int absolute)
        {
            var position = text.Lines.GetLinePosition(absolute);
            At(spans, path, position.Line + 1, position.Character);
        }

        AtIndex(element.Start + 1 + prefix);

        if (element.Start + element.Length > text.Length)
            return;

        var slice = text.ToString(new TextSpan(element.Start, element.Length));
        var closing = $"</{element.TagName}";

        for (var index = slice.IndexOf(closing, StringComparison.Ordinal);
             index >= 0;
             index = slice.IndexOf(closing, index + closing.Length, StringComparison.Ordinal))
            AtIndex(element.Start + index + 2 + prefix);
    }

    /// <summary>Authored positions of the symbol's declaration and every compiler reference, recovered through
    /// the generator's #line mapping. Positions the generator synthesizes, such as an @bind convention pair,
    /// have no authored place and are counted instead.</summary>
    public static async Task<(Dictionary<string, HashSet<(int Line, int Column)>> Spans, int Synthesized)> MappedSpansAsync(
        Solution solution,
        ISymbol symbol,
        bool markupOnly,
        CancellationToken cancellationToken)
    {
        Dictionary<string, HashSet<(int, int)>> spans = new(StringComparer.OrdinalIgnoreCase);
        var synthesized = 0;

        void Add(Location location)
        {
            var span = location.GetMappedLineSpan();

            if (!SourcePaths.IsActionable(span.Path) || (markupOnly && !MarkupMap.IsMarkup(span.Path)))
            {
                synthesized++;

                return;
            }

            At(spans, span.Path, span.StartLinePosition.Line + 1, span.StartLinePosition.Character);
        }

        foreach (var location in symbol.Locations.Where(location => location.IsInSource))
            Add(location);

        foreach (var referenced in await SymbolFinder.FindReferencesAsync(symbol, solution, cancellationToken))
        {
            foreach (var location in referenced.Locations.Where(location => !location.IsImplicit))
                Add(location.Location);
        }

        return (spans, synthesized);
    }

    /// <summary>Renders one file edit per touched file, replacing the name at exactly the given positions.</summary>
    public async Task<IReadOnlyList<FileEdit>> ComposeAsync(
        Solution solution,
        Dictionary<string, HashSet<(int Line, int Column)>> spans,
        string name,
        string newName,
        CancellationToken cancellationToken)
    {
        List<FileEdit> edits = [];

        foreach (var (path, positions) in spans.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            if (await TextAsync(solution, path, cancellationToken) is not { } text)
                continue;

            List<LineEdit> lines = [];

            foreach (var group in positions.GroupBy(position => position.Line).OrderBy(group => group.Key))
            {
                if (group.Key < 1 || group.Key > text.Lines.Count)
                    continue;

                var was = text.Lines[group.Key - 1].ToString();
                var now = was;

                foreach (var (_, column) in group.OrderByDescending(position => position.Column))
                {
                    if (column + name.Length > now.Length
                        || !string.Equals(now.Substring(column, name.Length), name, StringComparison.Ordinal))
                        continue;

                    now = now.Remove(column, name.Length).Insert(column, newName);
                }

                if (!string.Equals(was, now, StringComparison.Ordinal))
                    lines.Add(new LineEdit(group.Key, was, now));
            }

            if (lines.Count > 0)
                edits.Add(new FileEdit(paths.ToCaller(path), null, lines));
        }

        return edits;
    }

    public static void Merge(
        Dictionary<string, HashSet<(int Line, int Column)>> into,
        Dictionary<string, HashSet<(int Line, int Column)>> from)
    {
        foreach (var (path, positions) in from)
        {
            foreach (var position in positions)
                At(into, path, position.Line, position.Column);
        }
    }

    private static void At(
        Dictionary<string, HashSet<(int Line, int Column)>> spans,
        string path,
        int line,
        int column)
    {
        (spans.TryGetValue(path, out var positions) ? positions : spans[path] = []).Add((line, column));
    }

    private static async Task<SourceText?> TextAsync(Solution solution, string path, CancellationToken cancellationToken)
    {
        foreach (var project in solution.Projects)
        {
            TextDocument? document =
                project.Documents.FirstOrDefault(candidate =>
                    string.Equals(candidate.FilePath, path, StringComparison.OrdinalIgnoreCase))
                ?? project.AdditionalDocuments.FirstOrDefault(candidate =>
                    string.Equals(candidate.FilePath, path, StringComparison.OrdinalIgnoreCase));

            if (document is not null)
                return await document.GetTextAsync(cancellationToken);
        }

        return File.Exists(path) ? SourceText.From(await File.ReadAllTextAsync(path, cancellationToken)) : null;
    }
}
