namespace Snail.MCP.Roslyn.Domain;

public sealed record HealthReport(
    string Id,
    WorkspaceReadiness Readiness,
    int Projects,
    int Documents,
    long DiagnosticsMilliseconds,
    long WorkingSetMegabytes,
    int LoadedSolutions,
    bool ScriptingEnabled,
    IReadOnlyList<string> Notes);
