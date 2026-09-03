namespace Snail.MCP.Roslyn.Domain;

/// <summary>Outcome of opening a solution, including what the caller may and may not ask next.</summary>
public sealed record WorkspaceReport(
    string Id,
    WorkspaceReadiness Readiness,
    string Path,
    IReadOnlyList<LoadedProject> Projects,
    IReadOnlyList<string> Unloaded,
    int Errors,
    int Warnings,
    long ElapsedMilliseconds,
    IReadOnlyList<string> Notes);
