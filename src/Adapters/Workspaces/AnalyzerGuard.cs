namespace Snail.MCP.Roslyn.Adapters.Workspaces;

/// <summary>Decides which analyzer assemblies may run in this process, because an analyzer is arbitrary code.</summary>
/// <remarks>Policy shape adapted from roslyn-codelens-mcp (MIT, © Marcel Roozekrans).</remarks>
internal static class AnalyzerGuard
{
    private static readonly string PackageCache =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages");

    private static readonly string? SdkRoot = Path.GetDirectoryName(typeof(object).Assembly.Location);

    public static bool Allows(string policy, string? analyzerPath, string? solutionRoot)
    {
        if (string.Equals(policy, "all", StringComparison.OrdinalIgnoreCase))
            return true;

        if (analyzerPath is null)
            return false;

        var full = Path.GetFullPath(analyzerPath);

        if (Under(full, PackageCache) || (SdkRoot is not null && Under(full, SdkRoot)))
            return true;

        if (string.Equals(policy, "strict", StringComparison.OrdinalIgnoreCase))
            return false;

        return solutionRoot is not null && Under(full, solutionRoot);
    }

    private static bool Under(string path, string root) =>
        path.StartsWith(root, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
}
