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
        CancellationToken cancellationToken) =>
        await CompilerIssues.CollectAsync(
            await cache.FreshAsync(solutionId, cancellationToken),
            paths,
            string.IsNullOrWhiteSpace(scope) ? null : paths.ToProcess(scope),
            minimum,
            Math.Clamp(limit, 1, options.Value.MaxIssues),
            includeGenerated,
            cancellationToken);
}
