using Microsoft.Extensions.Configuration;

namespace Snail.MCP.Roslyn.Configuration;

public sealed class WorkspaceOptions
{
    /// <summary>Caller root mapped to process root; an empty map means the two coincide.</summary>
    public Dictionary<string, string> Roots { get; init; } = [];

    /// <summary>Folders this process may reach at all; empty means wherever it can, which is right for a
    /// server started for one caller and wrong for one shared by several.</summary>
    /// <remarks>A session opened at an address states its own root there, and this is what stands when it
    /// names none — the way a server run as a program is confined.</remarks>
    public string[] SessionRoots { get; init; } = [];

    /// <summary>Port to answer on; none means the server is started as a program and speaks stdio.</summary>
    /// <remarks>One knob decides the whole shape of the process: at an address it stands behind every worker of a
    /// machine and each session names the copy it works in, as a program it belongs to the one caller that started
    /// it and is confined by <see cref="SessionRoots"/>.</remarks>
    public int Port { get; init; }

    /// <summary>Whether load_solution restores packages itself when none are restored.</summary>
    /// <remarks>On, because nothing else in the arrangement does it: the worker clones the copy and never
    /// builds it, and without restore every semantic answer under-reports. Off for a deployment that restores
    /// by other means and would rather this server never started a process.</remarks>
    [ConfigurationKeyName("RestoreOnLoad")]
    public bool ShouldRestoreOnLoad { get; init; } = true;

    /// <summary>Seconds one restore may take; past it the load goes on unrestored and says so.</summary>
    /// <remarks>A cold package cache on a large solution is minutes, and a restore that hangs on an
    /// unreachable feed would otherwise hang the step that asked to load the solution.</remarks>
    public int RestoreSeconds { get; init; } = 300;

    public int MaxIssues { get; init; } = 200;

    public int MaxFindings { get; init; } = 200;

    /// <summary>How large a single answer may be, serialised. A client that cannot fit one cuts it, and a cut
    /// answer is unreadable JSON; the ceiling is below the size at which that was seen to happen.</summary>
    public int MaxResponseChars { get; init; } = 50_000;

    /// <summary>Scripts run in this process; the container, or the absence of one, is the only boundary.</summary>
    public bool AllowScripts { get; init; } = true;

    /// <summary>Where analyzer assemblies may be loaded from: 'strict' (package cache and the .NET installation only), 'default' (plus the solution's own bin), or 'all'.</summary>
    public string AnalyzerPolicy { get; init; } = "default";

    public int ScriptTimeoutSeconds { get; init; } = 30;

    /// <summary>Working set past which health_check advises a restart. Script and analyzer assemblies are never
    /// unloaded, so beyond some point only a new process gives the memory back.</summary>
    public int MemoryAdviceMegabytes { get; init; } = 2048;

    /// <summary>How many solutions stay loaded at once. Each holds its own compilations, so the ceiling is on
    /// memory; the one used longest ago is dropped and has to be loaded again.</summary>
    public int MaxSolutions { get; init; } = 4;

    /// <summary>How many steps one run_tools call may take; a run long enough to be a program belongs in run_script.</summary>
    public int MaxRunSteps { get; init; } = 10;
}
