using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Snail.MCP.Roslyn.Adapters.Workspaces;

/// <summary>The caller supplies the judgment — which lines, the renames, the @code, the replacement tag.
/// This supplies the guarantees: element boundaries checked by the Razor parser, and leaks named by
/// compiling the candidate in context.</summary>
internal sealed class RoslynExtractor(IPathMap paths, SolutionCache cache) : IComponentExtractor
{
    private const string PlanNote =
        "This plan never writes: create the new file from its edit, then delete the replacement's line range "
        + "in the source file and put the replacement text in its place. Run get_diagnostics afterwards — "
        + "no compile check covers the final wiring of tag attributes.";

    private const string UnresolvedNote =
        "The extracted markup references names that do not resolve inside the new component, so the plan "
        + "carries no edits: they could not compile as they stand. Map each name in 'substitutions' or declare "
        + "it in 'code' — as a [Parameter], usually — and call again.";

    private const string UnverifiedNote =
        "The Razor parser could not verify element boundaries here; the compile check on the candidate is the only guard.";

    private const string ExistsNote =
        "The target file already exists; applying the edit would replace its content.";

    private const string BaselineNote =
        "The source file already fails to compile, so some names listed may be pre-existing problems, not leaks.";

    private const string NoGeneratorNote =
        "The Razor generator produced no output for the candidate, so the leak check could not run: "
        + "'unresolved' is unknown here, not empty.";

    /// <summary>Error codes that name a symbol the candidate cannot see — the leak signal.</summary>
    private static readonly string[] LeakCodes = ["CS0103", "CS0246", "CS0234", "CS1061"];

    public async Task<ExtractionPlan> ExtractAsync(
        string solutionId,
        ExtractionRequest request,
        CancellationToken cancellationToken)
    {
        var solution = await cache.FreshAsync(solutionId, cancellationToken);
        var path = paths.ToProcess(request.File);

        var markup = Documents.FindMarkup(solution, path)
            ?? throw new InvalidOperationException(
                $"'{request.File}' is not a markup file of the loaded solution, so there is nothing to extract from.");

        var text = await markup.GetTextAsync(cancellationToken);

        if (request.StartLine < 1 || request.EndLine < request.StartLine || request.EndLine > text.Lines.Count)
            throw new ArgumentException(
                $"Lines {request.StartLine}-{request.EndLine} do not fit the file, which has {text.Lines.Count} lines.");

        List<string> notes = [];

        if (RazorMarkup.Parse(text, path) is { } elements)
        {
            if (Straddler(elements, text, request.StartLine, request.EndLine) is { } refusal)
                return new ExtractionPlan(request.Name, null, [], null, [], [refusal]);
        }
        else
        {
            notes.Add(UnverifiedNote);
        }

        var (content, indent) = Candidate(text, request);
        var unresolved = await LeaksAsync(solution, markup, content, notes, cancellationToken);

        var newPath = request.NewFile is { } chosen
            ? paths.ToProcess(chosen)
            : Path.Combine(Path.GetDirectoryName(path)!, $"{request.Name}.razor");

        if (File.Exists(newPath))
            notes.Add(ExistsNote);

        if (unresolved.Count > 0)
        {
            notes.Insert(0, UnresolvedNote);

            return new ExtractionPlan(request.Name, paths.ToCaller(newPath), [], null, unresolved, notes);
        }

        notes.Add(PlanNote);

        return new ExtractionPlan(
            request.Name,
            paths.ToCaller(newPath),
            [new FileEdit(paths.ToCaller(newPath), null, [], content)],
            Replacement(path, request, indent),
            unresolved,
            notes);
    }

    /// <summary>The new component's content: the block dedented, renames applied, the caller's @code appended.</summary>
    private static (string Content, string Indent) Candidate(SourceText text, ExtractionRequest request)
    {
        List<string> block =
        [
            .. Enumerable.Range(request.StartLine, request.EndLine - request.StartLine + 1)
                .Select(line => text.Lines[line - 1].ToString())
        ];

        var indent = block
            .Where(line => line.Trim().Length > 0)
            .Select(line => line.Length - line.TrimStart(' ').Length)
            .DefaultIfEmpty(0)
            .Min();

        IEnumerable<string> lines = block.Select(line => line.Length >= indent ? line[indent..] : line);

        foreach (var (find, replace) in (request.Substitutions ?? new Dictionary<string, string>())
                     .OrderByDescending(pair => pair.Key.Length))
            lines = lines.Select(line => line.Replace(find, replace, StringComparison.Ordinal));

        var content = string.Join('\n', lines);

        if (!string.IsNullOrWhiteSpace(request.Code))
            content = $"{content.TrimEnd('\n')}\n\n{request.Code.Trim('\n')}";

        return ($"{content.TrimEnd('\n')}\n", new string(' ', indent));
    }

    /// <summary>The page-side edit, compact: the tag re-indented to the block's own depth, never the whole file.</summary>
    private BlockReplacement Replacement(string path, ExtractionRequest request, string indent)
    {
        var tag = request.Tag ?? $"<{request.Name} />";

        var indented = string.Join(
            '\n',
            tag.Split('\n').Select(line => line.Length > 0 ? indent + line : line));

        return new BlockReplacement(paths.ToCaller(path), request.StartLine, request.EndLine, indented);
    }

    /// <summary>Names the identifiers that stop resolving once the block leaves its page, by compiling the
    /// candidate in the page's place: the compiler's answer, not a text guess.</summary>
    private async Task<IReadOnlyList<string>> LeaksAsync(
        Solution solution,
        TextDocument markup,
        string candidate,
        List<string> notes,
        CancellationToken cancellationToken)
    {
        if (await IsBaselineBrokenAsync(markup, cancellationToken))
            notes.Add(BaselineNote);

        var forked = solution.WithAdditionalDocumentText(markup.Id, SourceText.From(candidate));
        var project = forked.GetProject(markup.Project.Id)!;
        var compilation = await project.GetCompilationAsync(cancellationToken);
        var generated = await MarkupMap.FindGeneratedAsync(
            project, forked.GetAdditionalDocument(markup.Id)!, cancellationToken);

        if (compilation is null || generated is null)
        {
            notes.Add(NoGeneratorNote);

            return [];
        }

        var root = await generated.GetRootAsync(cancellationToken);
        SortedSet<string> unresolved = new(StringComparer.Ordinal);
        var others = 0;

        foreach (var diagnostic in compilation.GetDiagnostics(cancellationToken))
        {
            if (diagnostic.Severity != DiagnosticSeverity.Error || diagnostic.Location.SourceTree != generated)
                continue;

            if (LeakCodes.Contains(diagnostic.Id))
                unresolved.Add(root.FindToken(diagnostic.Location.SourceSpan.Start).ValueText);
            else if (++others <= 10)
                notes.Add($"Candidate error {diagnostic.Id}: {diagnostic.GetMessage()}");
        }

        return [.. unresolved];
    }

    private static async Task<bool> IsBaselineBrokenAsync(TextDocument markup, CancellationToken cancellationToken)
    {
        var compilation = await markup.Project.GetCompilationAsync(cancellationToken);
        var generated = await MarkupMap.FindGeneratedAsync(markup.Project, markup, cancellationToken);

        return compilation is not null
            && generated is not null
            && compilation.GetDiagnostics(cancellationToken).Any(diagnostic =>
                diagnostic.Severity == DiagnosticSeverity.Error && diagnostic.Location.SourceTree == generated);
    }

    /// <summary>An element the range would cut apart, phrased as the refusal the caller reads. Only an element
    /// with exactly one end inside the range loses its other half: one that contains the whole range is the
    /// usual case of lifting a block out of its container, and one lying fully inside or outside is no cut.</summary>
    private static string? Straddler(
        IReadOnlyList<RazorMarkup.Element> elements,
        SourceText text,
        int startLine,
        int endLine)
    {
        foreach (var element in elements)
        {
            if (element.Start + element.Length > text.Length)
                continue;

            var first = element.Line;
            var last = text.Lines.GetLinePosition(element.Start + element.Length - 1).Line + 1;

            var cutAtStart = first < startLine && last >= startLine && last <= endLine;
            var cutAtEnd = first >= startLine && first <= endLine && last > endLine;

            if (cutAtStart || cutAtEnd)
                return $"Element <{element.TagName}> spans lines {first}-{last}, crossing the requested range "
                    + $"{startLine}-{endLine}. Extraction would cut it apart; no edits are proposed. "
                    + "Align the range with whole elements and call again.";
        }

        return null;
    }
}
