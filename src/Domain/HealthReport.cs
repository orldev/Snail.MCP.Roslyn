using System.Text.Json.Serialization;

namespace Snail.MCP.Roslyn.Domain;

internal sealed record HealthReport(
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
    [property: JsonPropertyName("scriptingEnabled")] bool IsScriptingEnabled,
    IReadOnlyList<string> Notes);
