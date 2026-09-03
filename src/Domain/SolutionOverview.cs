namespace Snail.MCP.Roslyn.Domain;

/// <summary>The whole solution in one budgeted answer: graph, size, hotspots and what is already wrong.</summary>
public sealed record SolutionOverview(
    string Id,
    int Projects,
    int Documents,
    int Types,
    int Errors,
    int Warnings,
    IReadOnlyList<ProjectNode> Graph,
    IReadOnlyList<string> ProjectCycles,
    IReadOnlyList<SymbolMatch> Hotspots,
    IReadOnlyList<CodeIssue> TopIssues,
    IReadOnlyList<ArchitectureFinding> Architecture,
    IReadOnlyList<string> Notes);
