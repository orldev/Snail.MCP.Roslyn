namespace Snail.MCP.Roslyn.Domain;

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
