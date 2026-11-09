namespace Snail.MCP.Roslyn.Domain;

internal sealed record ProjectNode(
    string Name,
    string? TargetFramework,
    int Documents,
    int Types,
    int Errors,
    IReadOnlyList<string> DependsOn);
