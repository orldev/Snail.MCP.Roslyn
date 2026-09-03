using Microsoft.CodeAnalysis;

namespace Snail.MCP.Roslyn.Adapters.Workspaces;

/// <summary>One declaration together with everywhere the solution uses it.</summary>
internal sealed record UsageEntry(ISymbol Symbol)
{
    public int References { get; set; }

    public HashSet<string> Projects { get; } = [];

    public HashSet<string> Files { get; } = [];
}
