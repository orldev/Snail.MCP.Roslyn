namespace Snail.MCP.Roslyn.Domain;

internal sealed record SolutionOverview(
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
