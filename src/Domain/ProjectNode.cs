namespace Snail.MCP.Roslyn.Domain;

/// <summary>One project in the solution graph, with what it depends on.</summary>
public sealed record ProjectNode(
    string Name,
    string? TargetFramework,
    int Documents,
    int Types,
    int Errors,
    IReadOnlyList<string> DependsOn);
