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

    public static bool Covers(string filter, string? file) =>
        file is not null && file.StartsWith(filter, StringComparison.Ordinal);
}
