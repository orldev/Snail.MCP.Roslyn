namespace Snail.MCP.Roslyn.Domain;

public sealed record UnloadReport(string Id, bool Unloaded, IReadOnlyList<string> Notes);
