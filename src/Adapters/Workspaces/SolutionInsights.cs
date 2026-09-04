using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;

namespace Snail.MCP.Roslyn.Adapters.Workspaces;

/// <summary>Caches the expensive solution-wide passes per snapshot, so a second question costs nothing.</summary>
/// <remarks>A snapshot is immutable, so the cache needs no invalidation: an edit produces a new snapshot and the old
/// entry dies with it. The walk behind it is cheaper still, keeping what it read per compilation.</remarks>
internal static class SolutionInsights
{
    private static readonly ConditionalWeakTable<Solution, Task<SolutionFacts>> BySnapshot = [];

    public static Task<SolutionFacts> FactsAsync(Solution solution, CancellationToken cancellationToken) =>
        BySnapshot.GetValue(solution, snapshot => SolutionFacts.BuildAsync(snapshot, cancellationToken));
}
