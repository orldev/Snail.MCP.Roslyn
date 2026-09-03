namespace Snail.MCP.Roslyn.Domain;

/// <summary>Whether the server itself is in a state where its answers can be trusted.</summary>
public sealed record HealthReport(
    string Id,
    WorkspaceReadiness Readiness,
    int Projects,
    int Documents,
    long DiagnosticsMilliseconds,
    long WorkingSetMegabytes,
    bool ScriptingEnabled,
    IReadOnlyList<string> Notes);
