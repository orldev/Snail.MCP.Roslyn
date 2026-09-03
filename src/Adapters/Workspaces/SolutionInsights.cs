using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;

namespace Snail.MCP.Roslyn.Adapters.Workspaces;

/// <summary>Caches the expensive solution-wide passes per snapshot, so a second question costs nothing.</summary>
/// <remarks>A snapshot is immutable, so the cache needs no invalidation: an edit produces a new snapshot and the old entry dies with it.</remarks>
internal static class SolutionInsights
{
    private static readonly ConditionalWeakTable<Solution, Task<SolutionUsage>> UsageBySnapshot = [];

    private static readonly ConditionalWeakTable<Solution, Task<NamespaceGraph>> GraphBySnapshot = [];

    public static Task<SolutionUsage> UsageAsync(Solution solution, CancellationToken cancellationToken) =>
        UsageBySnapshot.GetValue(solution, snapshot => SolutionUsage.BuildAsync(snapshot, cancellationToken));

    public static Task<NamespaceGraph> GraphAsync(Solution solution, CancellationToken cancellationToken) =>
        GraphBySnapshot.GetValue(solution, snapshot => NamespaceGraph.BuildAsync(snapshot, cancellationToken));
}
