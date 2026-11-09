using Microsoft.Extensions.Options;
using Snail.MCP.Roslyn.Configuration;

namespace Snail.MCP.Roslyn.Adapters.Workspaces;

/// <summary>What the compiler says about a loaded solution, and on request what the project's own analyzers say.</summary>
/// <remarks>
/// The compiler alone is the cheap answer, and on most projects the whole one. On a project whose warnings are errors and
/// whose analyzers — style rules, threading, the SDK's — run in the build, code that compiles still fails it: the
/// answer was empty while <c>dotnet build</c> failed on IDE0005 and VSTHRD103 (2026-10-17), and an agent told to trust it
/// instead of the build trusted it. Such an answer says that the analyzers were not run.
/// </remarks>
internal sealed class RoslynDiagnostics(IPathMap paths, SolutionCache cache, IOptions<WorkspaceOptions> options)
    : IDiagnosticReader
{
    private const string UnrunNote =
        "The project's analyzers were not run, and its build treats warnings as errors: they can fail the build on code "
        + "that compiles, so a clean list here does not mean the build passes. Pass analyzers=true, with the file you "
        + "changed as scope, to see what the build will say.";

    public async Task<IssuePage> ReadAsync(
        string solutionId,
        string? scope,
        IssueSeverity minimum,
        int limit,
        bool includeGenerated,
        bool analyzers,
        CancellationToken cancellationToken)
    {
        var solution = await cache.FreshAsync(solutionId, cancellationToken);
        var filter = string.IsNullOrWhiteSpace(scope) ? null : paths.ToProcess(scope);

        var page = await CompilerIssues.CollectAsync(
            solution,
            paths,
            filter,
            minimum,
            Math.Clamp(limit, 1, options.Value.MaxIssues),
            includeGenerated,
            analyzers ? options.Value.AnalyzerPolicy : null,
            cancellationToken);

        if (analyzers || !CompilerIssues.Asked(solution, filter).Any(ProjectAnalyzers.CanFailTheBuild))
            return page;

        return page with { Notes = [UnrunNote] };
    }
}
