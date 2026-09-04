using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Options;
using Snail.MCP.Roslyn.Configuration;

namespace Snail.MCP.Roslyn.Adapters.Workspaces;

internal sealed class RoslynDiagnostics(IPathMap paths, SolutionCache cache, IOptions<WorkspaceOptions> options)
    : IDiagnosticReader
{
    public async Task<IssuePage> ReadAsync(
        string solutionId,
        string? scope,
        IssueSeverity minimum,
        int limit,
        bool includeGenerated,
        CancellationToken cancellationToken)
    {
        var solution = await cache.FreshAsync(solutionId, cancellationToken);
        var filter = string.IsNullOrWhiteSpace(scope) ? null : paths.ToProcess(scope);
        var cap = Math.Clamp(limit, 1, options.Value.MaxIssues);

        Dictionary<(string Id, string? File, int Line), CodeIssue> collected = [];
        Dictionary<(string Id, string? File, int Line), int> repeats = [];
        var total = 0;

        foreach (var project in solution.Projects.Where(project => project.Language == LanguageNames.CSharp))
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

                var issue = Locate(diagnostic, source);
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

    private static IssueSeverity Rank(DiagnosticSeverity severity) => severity switch
    {
        DiagnosticSeverity.Error => IssueSeverity.Error,
        DiagnosticSeverity.Warning => IssueSeverity.Warning,
        _ => IssueSeverity.Info
    };

    private CodeIssue Locate(Diagnostic diagnostic, string? source)
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
