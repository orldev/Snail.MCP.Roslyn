namespace Snail.MCP.Roslyn.Domain;

public sealed record ArchitectureReport(
    IReadOnlyList<ArchitectureFinding> Findings,
    int Total,
    bool Truncated,
    IReadOnlyList<string> Notes);
