namespace Snail.MCP.Roslyn.Domain;

internal sealed record ImpactSite(string Project, int References, IReadOnlyList<string> Files);
