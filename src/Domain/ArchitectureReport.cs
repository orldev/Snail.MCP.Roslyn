namespace Snail.MCP.Roslyn.Domain;

/// <summary>Structural breaches found across the solution; truncation is always stated.</summary>
public sealed record ArchitectureReport(
    IReadOnlyList<ArchitectureFinding> Findings,
    int Total,
    bool Truncated,
    IReadOnlyList<string> Notes);
