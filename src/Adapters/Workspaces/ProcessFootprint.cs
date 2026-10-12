using System.Reflection;

namespace Snail.MCP.Roslyn.Adapters.Workspaces;

/// <summary>What this process is holding, and how much of it it can never give back.</summary>
/// <remarks>A server that answers all day accumulates two things no collection reclaims: the assembly every
/// run_script emits, and the analyzer assemblies loaded on behalf of the solutions it opened. Neither is
/// unloadable — both land in the default load context — so the only remedy is a restart, and the only way a
/// caller can know it is due is to be told the numbers.</remarks>
internal static class ProcessFootprint
{
    public static long WorkingSetMegabytes => Environment.WorkingSet / (1024 * 1024);

    public static long ManagedHeapMegabytes => GC.GetTotalMemory(false) / (1024 * 1024);

    public static int Assemblies => AppDomain.CurrentDomain.GetAssemblies().Length;

    /// <summary>Assemblies loaded from outside this server and outside the .NET installation: the analyzers and
    /// generators the analysed solutions brought with them.</summary>
    public static int Foreign =>
        AppDomain.CurrentDomain.GetAssemblies().Count(assembly => IsForeign(assembly));

    private static bool IsForeign(Assembly assembly)
    {
        if (assembly.IsDynamic)
            return false;

        var location = assembly.Location;

        return !string.IsNullOrEmpty(location) && !AnalyzerGuard.ShipsWithTheServer(location);
    }
}

/// <summary>How many scripts have run since the process started.</summary>
/// <remarks>Counted rather than derived from the loaded assemblies: the submission assemblies are named by a
/// convention Roslyn does not promise, and a health report is the wrong place to depend on one.</remarks>
internal sealed class ScriptTally
{
    private int _ran;

    public int Ran => Volatile.Read(ref _ran);

    public void Counted() => Interlocked.Increment(ref _ran);
}
