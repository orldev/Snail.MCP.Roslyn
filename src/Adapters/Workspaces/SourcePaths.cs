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

    public static bool IsUnderOutput(string file) => Buried(file, "obj") || Buried(file, "bin");

    /// <summary>True when a folder of this name lies on the path with at least one more folder below it, which is
    /// the shape build output has: obj/Debug/net10.0/x.g.cs, bin/Debug/net10.0/x.dll.</summary>
    /// <remarks>A folder of the same name holding sources directly is the author's. The SDK excludes only the one
    /// at the project's own root and compiles everything under any other, so calling Features/bin/Item.cs build
    /// output hides the compiler's own errors about a file the caller wrote and has to fix.</remarks>
    private static bool Buried(string file, string folder)
    {
        var marker = $"{Path.DirectorySeparatorChar}{folder}{Path.DirectorySeparatorChar}";

        for (var index = file.IndexOf(marker, StringComparison.Ordinal);
             index >= 0;
             index = file.IndexOf(marker, index + marker.Length, StringComparison.Ordinal))
        {
            if (file.IndexOf(Path.DirectorySeparatorChar, index + marker.Length) >= 0)
                return true;
        }

        return false;
    }

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
