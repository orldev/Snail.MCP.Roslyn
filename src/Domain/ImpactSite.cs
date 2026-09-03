namespace Snail.MCP.Roslyn.Domain;

/// <summary>How much of one project depends on the symbol under question.</summary>
public sealed record ImpactSite(string Project, int References, IReadOnlyList<string> Files);
