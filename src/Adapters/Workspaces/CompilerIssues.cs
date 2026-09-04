using Microsoft.CodeAnalysis;

namespace Snail.MCP.Roslyn.Adapters.Workspaces;

/// <summary>Collects what the compiler says, from a solution snapshot the caller already holds.</summary>
/// <remarks>Held apart from the port so an answer assembled from one snapshot takes its issues from that same
/// snapshot rather than asking for another: a second refresh inside one answer can number a file's text from
/// one version and report its issues from the next, and it repeats the whole staleness scan to do it.</remarks>
internal static class CompilerIssues
{
    public static async Task<IssuePage> CollectAsync(
        Solution solution,
        IPathMap paths,
        string? filter,
        IssueSeverity minimum,
        int cap,
        bool includeGenerated,
        CancellationToken cancellationToken)
    {
        Dictionary<(string Id, string? File, int Line), CodeIssue> collected = [];
        Dictionary<(string Id, string? File, int Line), int> repeats = [];
        var total = 0;

        foreach (var project in Asked(solution, filter))
        {
            var compilation = await project.GetCompilationAsync(cancellationToken)
                ?? throw new InvalidOperationException(
                    $"Project '{project.Name}' produced no compilation; diagnostics would be silently empty.");

            foreach (var diagnostic in compilation.GetDiagnostics(cancellationToken))
            {
                if (Rank(diagnostic.Severity) < minimum)
                    continue;

                var source = SourcePaths.Of(diagnostic);

                if (!includeGenerated && !SourcePaths.IsActionable(source))
                    continue;

                if (filter is not null && !SourcePaths.Covers(filter, source))
                    continue;

                var issue = Locate(paths, diagnostic, source);
                var key = (issue.Id, issue.File, issue.Line);

                total++;
                repeats[key] = repeats.GetValueOrDefault(key) + 1;

                if (!collected.ContainsKey(key) && collected.Count < cap)
                    collected[key] = issue;
            }
        }

        List<CodeIssue> items = [.. collected.Select(pair => pair.Value with { Repeats = repeats[pair.Key] })];

        return new IssuePage(items, total, total > items.Count);
    }

    /// <summary>The projects that can produce a diagnostic the filter keeps.</summary>
    /// <remarks>A diagnostic is located in a tree of its own compilation, so a project holding no file under
    /// the filter can only produce issues the filter throws away — and compiling it is what made asking about
    /// one file cost as much as asking about the whole solution. Holding no such file does not prove a project
    /// cannot report on the path, though: a path this snapshot has no document for may still be generated code
    /// some compilation maps back to, so an unrecognised scope falls back to asking everyone, as it always did.</remarks>
    private static IReadOnlyList<Project> Asked(Solution solution, string? filter)
    {
        List<Project> csharp = [.. solution.Projects.Where(project => project.Language == LanguageNames.CSharp)];

        if (filter is null)
            return csharp;

        List<Project> holding = [.. csharp.Where(project => Holds(project, filter))];

        return holding.Count > 0 ? holding : csharp;
    }

    private static bool Holds(Project project, string filter) =>
        project.Documents.Concat<TextDocument>(project.AdditionalDocuments)
            .Any(document => SourcePaths.Covers(filter, document.FilePath));

    private static IssueSeverity Rank(DiagnosticSeverity severity) => severity switch
    {
        DiagnosticSeverity.Error => IssueSeverity.Error,
        DiagnosticSeverity.Warning => IssueSeverity.Warning,
        _ => IssueSeverity.Info
    };

    private static CodeIssue Locate(IPathMap paths, Diagnostic diagnostic, string? source)
    {
        var span = diagnostic.Location.GetMappedLineSpan();

        return new CodeIssue(
            diagnostic.Id,
            Rank(diagnostic.Severity),
            diagnostic.GetMessage(),
            source is null ? null : paths.ToCaller(source),
            span.StartLinePosition.Line + 1,
            span.StartLinePosition.Character + 1,
            1);
    }
}
