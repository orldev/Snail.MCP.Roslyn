namespace Snail.MCP.Roslyn.Domain;

/// <summary>One project as the workspace loaded it.</summary>
public sealed record LoadedProject(
    string Name,
    string? TargetFramework,
    int Documents,
    int References,
    bool HasSemantics);
