namespace Snail.MCP.Roslyn.Configuration;

public sealed class WorkspaceOptions
{
    public const string Section = "Workspace";

    /// <summary>Caller root mapped to process root; an empty map means the two coincide.</summary>
    public Dictionary<string, string> Roots { get; init; } = [];

    public int MaxIssues { get; init; } = 200;

    public int MaxFindings { get; init; } = 200;

    /// <summary>Scripts run in this process; the container, or the absence of one, is the only boundary.</summary>
    public bool AllowScripts { get; init; } = true;

    /// <summary>Where analyzer assemblies may be loaded from: 'strict' (package cache and SDK only), 'default' (plus the solution's own bin), or 'all'.</summary>
    public string AnalyzerPolicy { get; init; } = "default";

    public int ScriptTimeoutSeconds { get; init; } = 30;

    /// <summary>How many solutions stay loaded at once. Each holds its own compilations, so the ceiling is on
    /// memory; the one used longest ago is dropped and has to be loaded again.</summary>
    public int MaxSolutions { get; init; } = 4;
}
