namespace Snail.MCP.Roslyn.Domain;

public sealed record ImpactSite(string Project, int References, IReadOnlyList<string> Files);
