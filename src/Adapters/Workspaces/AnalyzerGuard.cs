using Microsoft.Build.Locator;

namespace Snail.MCP.Roslyn.Adapters.Workspaces;

/// <summary>Decides which analyzer assemblies may run in this process, because an analyzer is arbitrary code.</summary>
/// <remarks>Policy shape adapted from roslyn-codelens-mcp (MIT, © Marcel Roozekrans).</remarks>
internal static class AnalyzerGuard
{
    private static readonly string PackageCache =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages");

    /// <summary>Where .NET itself is installed. A plain project's analyzers come from two places inside it —
    /// sdk/&lt;version&gt;/Sdks/.../analyzers and packs/Microsoft.NETCore.App.Ref/.../analyzers, the runtime's own
    /// generators — and from neither the package cache nor this process's runtime directory, which is what the
    /// trust root used to be derived from. Everything under the installation ships with .NET itself.</summary>
    private static readonly IReadOnlyList<string> DotnetRoots = Installed();

    public static bool Allows(string policy, string? analyzerPath, string? solutionRoot)
    {
        if (string.Equals(policy, "all", StringComparison.OrdinalIgnoreCase))
            return true;

        if (analyzerPath is null)
            return false;

        var full = Path.GetFullPath(analyzerPath);

        if (Under(full, PackageCache) || DotnetRoots.Any(root => Under(full, root)))
            return true;

        if (string.Equals(policy, "strict", StringComparison.OrdinalIgnoreCase))
            return false;

        return solutionRoot is not null && Under(full, solutionRoot);
    }

    /// <summary>True when the assembly at this path came with .NET or with this server, rather than with a
    /// solution it was asked to analyse.</summary>
    public static bool ShipsWithTheServer(string path) =>
        Under(path, AppContext.BaseDirectory) || DotnetRoots.Any(root => Under(path, root));

    /// <summary>The installation behind every SDK MSBuild knows about, and this process's own as a fallback for
    /// when MSBuild was never located.</summary>
    private static IReadOnlyList<string> Installed()
    {
        HashSet<string> roots = new(StringComparer.Ordinal);

        foreach (var instance in Located())
        {
            // .../dotnet/sdk/10.0.202 -> .../dotnet
            if (Above(instance, 2) is { } installation)
                roots.Add(installation);
        }

        // .../dotnet/shared/Microsoft.NETCore.App/10.0.6 -> .../dotnet
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

    private static bool Under(string path, string root) => SourcePaths.IsUnder(path, root);
}
