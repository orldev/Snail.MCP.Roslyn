namespace Snail.MCP.Roslyn.Domain;

public sealed record HealthReport(
    string Id,
    WorkspaceReadiness Readiness,
    int Projects,
    int Documents,
    long DiagnosticsMilliseconds,
    long WorkingSetMegabytes,
    long ManagedHeapMegabytes,
    int AssembliesLoaded,
    int ForeignAssemblies,
    int ScriptsRun,
    int LoadedSolutions,
    bool ScriptingEnabled,
    IReadOnlyList<string> Notes);
