using Microsoft.Build.Locator;
using Snail.Sdk.Mcp.Hosting;

namespace Snail.MCP.Roslyn.Adapters.Workspaces;

/// <summary>Decides which analyzer assemblies may run in this process, because an analyzer is arbitrary code.</summary>
/// <remarks>Policy shape adapted from roslyn-codelens-mcp (MIT, © Marcel Roozekrans).</remarks>
internal static class AnalyzerGuard
{
    /// <summary>The variable NuGet reads its package cache from before it falls back to the one in the user's home.</summary>
    private const string CacheVariable = "NUGET_PACKAGES";

    /// <summary>
    /// The package cache restore fills, where a project's own analyzers come from. It was taken from the user's home
    /// alone, while the image sets <c>NUGET_PACKAGES=/nuget</c> for the mounted cache: in a container every analyzer a
    /// project restored was refused as one from outside, and <c>run_analyzers</c> and <c>get_diagnostics</c> answered
    /// without the project's own. A <c>globalPackagesFolder</c> in a nuget.config is not read; such a cache is the
    /// solution's or the policy <c>all</c>'s.
    /// </summary>
    private static readonly string PackageCache = CacheOf(Environment.GetEnvironmentVariable(CacheVariable));

    /// <summary>Where .NET itself is installed. A plain project's analyzers come from two places inside it —
    /// sdk/&lt;version&gt;/Sdks/.../analyzers and packs/Microsoft.NETCore.App.Ref/.../analyzers, the runtime's own
    /// generators — and from neither the package cache nor this process's runtime directory, which is what the
    /// trust root used to be derived from. Everything under the installation ships with .NET itself.</summary>
    private static readonly IReadOnlyList<string> DotnetRoots = Installed();

    public static bool Allows(string policy, string? analyzerPath, string? solutionRoot) =>
        Allows(policy, analyzerPath, solutionRoot, PackageCache);

    /// <summary>Whether the policy lets the analyzer at <paramref name="analyzerPath"/> run, with the package cache at <paramref name="packageCache"/>.</summary>
    public static bool Allows(string policy, string? analyzerPath, string? solutionRoot, string packageCache)
    {
        if (string.Equals(policy, "all", StringComparison.OrdinalIgnoreCase))
            return true;

        if (analyzerPath is null)
            return false;

        var full = Path.GetFullPath(analyzerPath);

        if (IsUnder(full, packageCache) || DotnetRoots.Any(root => IsUnder(full, root)))
            return true;

        if (string.Equals(policy, "strict", StringComparison.OrdinalIgnoreCase))
            return false;

        return solutionRoot is not null && IsUnder(full, solutionRoot);
    }

    /// <summary>The package cache NuGet uses with <paramref name="configured"/> as its variable: that folder, or the one in the user's home.</summary>
    public static string CacheOf(string? configured) =>
        string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(ServerFolders.Home, ".nuget", "packages")
            : ServerFolders.FullPath(configured);

    /// <summary>True when the assembly at this path came with .NET or with this server, rather than with a
    /// solution it was asked to analyse.</summary>
    public static bool ShipsWithTheServer(string path) =>
        IsUnder(path, AppContext.BaseDirectory) || DotnetRoots.Any(root => IsUnder(path, root));

    /// <summary>The installation behind every SDK MSBuild knows about, and this process's own as a fallback for
    /// when MSBuild was never located.</summary>
    /// <remarks>An SDK MSBuild locates sits two folders below its installation (.../dotnet/sdk/10.0.202), the running
    /// runtime three (.../dotnet/shared/Microsoft.NETCore.App/10.0.6).</remarks>
    private static IReadOnlyList<string> Installed()
    {
        HashSet<string> roots = new(StringComparer.Ordinal);

        foreach (var instance in Located())
        {
            if (Above(instance, 2) is { } installation)
                roots.Add(installation);
        }

        if (roots.Count == 0 && Above(Path.GetDirectoryName(typeof(object).Assembly.Location), 3) is { } running)
            roots.Add(running);

        return [.. roots];
    }

    private static IEnumerable<string> Located()
    {
        try
        {
            return [.. MSBuildLocator.QueryVisualStudioInstances().Select(instance => instance.MSBuildPath)];
        }
        catch (Exception)
        {
            return [];
        }
    }

    private static string? Above(string? path, int levels)
    {
        for (var level = 0; level < levels && !string.IsNullOrEmpty(path); level++)
            path = Path.GetDirectoryName(path);

        return string.IsNullOrEmpty(path) ? null : path;
    }

    private static bool IsUnder(string path, string root) => SourcePaths.IsUnder(path, root);
}
