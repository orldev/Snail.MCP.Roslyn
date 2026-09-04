using System.Collections.Immutable;
using System.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.Extensions.Options;
using Snail.MCP.Roslyn.Configuration;

namespace Snail.MCP.Roslyn.Adapters.Workspaces;

internal sealed class RoslynAnalyzers(IPathMap paths, SolutionCache cache, IOptions<WorkspaceOptions> options)
    : IAnalyzerRunner
{
    private const string CostNote =
        "Analyzers cost seconds, not milliseconds: they re-walk every syntax tree. "
        + "Use get_diagnostics for the after-every-edit loop and this tool when you are looking for a pattern.";

    private const string ShippedNote =
        "This project references no analyzers of its own, so the ones shipped with this server were used. "
        + "Their findings are suggestions from a general rule set, not the project's own standard.";

    private const string BlockedNote =
        "Some analyzer assemblies were not run: they live outside the package cache, the .NET installation and "
        + "the solution, "
        + "and an analyzer is arbitrary code executed in this process. Set Workspace:AnalyzerPolicy to 'all' to run them anyway.";

    private const string FixNote =
        "Fixes are proposals from the compiler's own providers, returned as edits. This tool never writes to disk. "
        + "A fix that compiles is not automatically a fix that is right.";

    private readonly SolutionEdits editor = new(paths);

    public async Task<AnalyzerPage> RunAsync(
        string solutionId,
        string? scope,
        string? category,
        IssueSeverity minimum,
        int limit,
        CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        var solution = await cache.FreshAsync(solutionId, cancellationToken);
        var filter = string.IsNullOrWhiteSpace(scope) ? null : paths.ToProcess(scope);
        var cap = Math.Clamp(limit, 1, options.Value.MaxIssues);

        Dictionary<(string Id, string? File, int Line), AnalyzerIssue> collected = [];
        Dictionary<(string Id, string? File, int Line), int> repeats = [];
        HashSet<string> used = [];
        var shipped = false;
        var blocked = false;
        var total = 0;

        foreach (var project in solution.Projects.Where(project => project.Language == LanguageNames.CSharp))
        {
            var compilation = await project.GetCompilationAsync(cancellationToken);

            if (compilation is null)
                continue;

            var analyzers = Select(project, options.Value.AnalyzerPolicy, ref shipped, ref blocked);

            if (analyzers.Length == 0)
                continue;

            used.UnionWith(analyzers.Select(analyzer => analyzer.GetType().Name));

            var found = await compilation
                .WithAnalyzers(analyzers, project.AnalyzerOptions)
                .GetAnalyzerDiagnosticsAsync(cancellationToken);

            foreach (var diagnostic in found)
            {
                if (Rank(diagnostic.Severity) < minimum)
                    continue;

                if (category is not null && !diagnostic.Descriptor.Category.Contains(category, StringComparison.OrdinalIgnoreCase))
                    continue;

                var source = SourcePaths.Of(diagnostic);

                if (!SourcePaths.IsActionable(source))
                    continue;

                if (filter is not null && !SourcePaths.Covers(filter, source))
                    continue;

                var issue = Describe(diagnostic, source);
                var key = (issue.Id, issue.File, issue.Line);

                total++;
                repeats[key] = repeats.GetValueOrDefault(key) + 1;

                if (!collected.ContainsKey(key) && collected.Count < cap)
                    collected[key] = issue;
            }
        }

        List<AnalyzerIssue> items = [.. collected.Select(pair => pair.Value with { Repeats = repeats[pair.Key] })];

        List<string> notes = [CostNote];

        if (shipped)
            notes.Add(ShippedNote);

        if (blocked)
            notes.Add(BlockedNote);

        return new AnalyzerPage(
            items,
            [.. used.Order(StringComparer.Ordinal).Take(50)],
            total,
            total > items.Count,
            (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds,
            notes);
    }

    public async Task<CodeFixPage> FixesAsync(
        string solutionId,
        string file,
        int line,
        string? diagnosticId,
        CancellationToken cancellationToken)
    {
        var solution = await cache.FreshAsync(solutionId, cancellationToken);
        var path = paths.ToProcess(file);
        var documentId = solution.GetDocumentIdsWithFilePath(path).FirstOrDefault();

        if (documentId is null || solution.GetDocument(documentId) is not { } document)
            return new CodeFixPage(diagnosticId ?? string.Empty, string.Empty, [], ["That file is not part of the loaded solution."]);

        var diagnostic = await LocateAsync(document, line, diagnosticId, cancellationToken);

        if (diagnostic is null)
            return new CodeFixPage(diagnosticId ?? string.Empty, string.Empty, [],
                ["No diagnostic was reported on that line. Call get_diagnostics to see where the compiler actually points."]);

        List<CodeFix> fixes = [];

        foreach (var provider in CodeFixProviders.For(diagnostic.Id))
        {
            List<CodeAction> actions = [];
            var context = new CodeFixContext(document, diagnostic, (action, _) => actions.Add(action), cancellationToken);

            try
            {
                await provider.RegisterCodeFixesAsync(context);
            }
            catch (Exception)
            {
                continue;
            }

            foreach (var action in actions)
            {
                var edits = await EditsAsync(solution, action, cancellationToken);

                if (edits.Count > 0)
                    fixes.Add(new CodeFix(action.Title, provider.GetType().Name, edits));
            }
        }

        return new CodeFixPage(diagnostic.Id, diagnostic.GetMessage(), fixes, Explain(diagnostic.Id, fixes.Count));
    }

    /// <summary>An empty answer must say whether nobody offered a fix or nobody was asked.</summary>
    private static IReadOnlyList<string> Explain(string diagnosticId, int fixes)
    {
        if (fixes > 0)
            return [FixNote];

        var claimed = CodeFixProviders.For(diagnosticId).Count();

        return claimed > 0
            ? [$"{claimed} provider(s) handle {diagnosticId} but offered nothing here; the repair depends on intent the compiler cannot infer."]
            : [$"No provider loaded in this process repairs {diagnosticId}. Providers available: {CodeFixProviders.Count}. Fix it by hand."];
    }

    private async Task<IReadOnlyList<FileEdit>> EditsAsync(Solution before, CodeAction action, CancellationToken cancellationToken)
    {
        try
        {
            var operations = await action.GetOperationsAsync(cancellationToken);
            var applied = operations.OfType<ApplyChangesOperation>().FirstOrDefault();

            return applied is null ? [] : await editor.BetweenAsync(before, applied.ChangedSolution, cancellationToken);
        }
        catch (Exception)
        {
            return [];
        }
    }

    private static async Task<Diagnostic?> LocateAsync(
        Document document,
        int line,
        string? diagnosticId,
        CancellationToken cancellationToken)
    {
        var compilation = await document.Project.GetCompilationAsync(cancellationToken);
        var tree = await document.GetSyntaxTreeAsync(cancellationToken);

        if (compilation is null || tree is null)
            return null;

        return compilation.GetDiagnostics(cancellationToken)
            .Where(diagnostic => diagnostic.Location.SourceTree == tree)
            .Where(diagnostic => diagnosticId is null || diagnostic.Id.Equals(diagnosticId, StringComparison.OrdinalIgnoreCase))
            .FirstOrDefault(diagnostic => diagnostic.Location.GetMappedLineSpan().StartLinePosition.Line + 1 == line);
    }

    private static ImmutableArray<DiagnosticAnalyzer> Select(Project project, string policy, ref bool shipped, ref bool blocked)
    {
        var root = project.FilePath is { } file ? Path.GetDirectoryName(file) : null;
        List<DiagnosticAnalyzer> own = [];

        foreach (var reference in project.AnalyzerReferences)
        {
            if (!AnalyzerGuard.Allows(policy, reference.FullPath, root))
            {
                blocked = true;

                continue;
            }

            own.AddRange(reference.GetAnalyzers(LanguageNames.CSharp));
        }

        if (own.Count > 0)
            return [.. own];

        shipped = true;

        return ShippedAnalyzers.All;
    }

    private static IssueSeverity Rank(DiagnosticSeverity severity) => severity switch
    {
        DiagnosticSeverity.Error => IssueSeverity.Error,
        DiagnosticSeverity.Warning => IssueSeverity.Warning,
        _ => IssueSeverity.Info
    };

    private AnalyzerIssue Describe(Diagnostic diagnostic, string? source)
    {
        var span = diagnostic.Location.GetMappedLineSpan();

        return new AnalyzerIssue(
            diagnostic.Id,
            diagnostic.Descriptor.Category,
            Rank(diagnostic.Severity),
            diagnostic.GetMessage(),
            source is null ? null : paths.ToCaller(source),
            span.StartLinePosition.Line + 1,
            span.StartLinePosition.Character + 1,
            1);
    }
}
