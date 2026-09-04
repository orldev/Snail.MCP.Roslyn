using Microsoft.CodeAnalysis;

namespace Snail.MCP.Roslyn.Adapters.Workspaces;

/// <summary>Tells source a caller can edit from code only the build produces.</summary>
internal static class SourcePaths
{
    public static string? Of(Diagnostic diagnostic)
    {
        var mapped = diagnostic.Location.GetMappedLineSpan().Path;

        return string.IsNullOrEmpty(mapped) ? diagnostic.Location.SourceTree?.FilePath : mapped;
    }

    public static bool IsActionable(string? file) =>
        !string.IsNullOrEmpty(file) && Path.IsPathRooted(file) && !IsUnderOutput(file);

    public static bool IsUnderOutput(string file) =>
        file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
        || file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal);

    public static bool Covers(string filter, string? file) => IsUnder(file, filter);

    /// <summary>True when the path is the root itself or lies inside it.</summary>
    /// <remarks>A bare prefix test also matches a sibling whose name merely begins the same way — /src/Foo
    /// would cover /src/FooBar, and ~/.nuget/packages would cover ~/.nuget/packages-anything, which is the
    /// difference between trusting an analyzer assembly and running arbitrary code from an unexpected place.</remarks>
    public static bool IsUnder(string? path, string? root)
    {
        if (path is null || string.IsNullOrEmpty(root))
            return false;

        var comparison = OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

        if (path.Equals(root, comparison))
            return true;

        var bounded = root[^1] is '/' or '\\' ? root : root + Path.DirectorySeparatorChar;

        return path.StartsWith(bounded, comparison);
    }
}
