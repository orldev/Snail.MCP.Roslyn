namespace Snail.MCP.Roslyn.Domain;

/// <summary>Outcome of releasing a loaded solution and the memory it held.</summary>
public sealed record UnloadReport(string Id, bool Unloaded, IReadOnlyList<string> Notes);
