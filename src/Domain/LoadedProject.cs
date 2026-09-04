namespace Snail.MCP.Roslyn.Domain;

public sealed record LoadedProject(
    string Name,
    string? TargetFramework,
    int Documents,
    int References,
    bool HasSemantics);
