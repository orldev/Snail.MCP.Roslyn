namespace Snail.MCP.Roslyn.Domain;

internal sealed record LoadedProject(
    string Name,
    string? TargetFramework,
    int Documents,
    int References,
    bool HasSemantics);
