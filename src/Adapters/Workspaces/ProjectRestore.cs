using Microsoft.CodeAnalysis;

namespace Snail.MCP.Roslyn.Adapters.Workspaces;

/// <summary>Tells whether package restore has run, by the file it leaves behind rather than by a proxy.</summary>
internal static class ProjectRestore
{
    private const string Assets = "project.assets.json";

    public static bool HasRun(Project project)
    {
        if (project.FilePath is not { } file || Path.GetDirectoryName(file) is not { } root)
            return true;

        return File.Exists(Path.Combine(root, "obj", Assets));
    }

    public static IReadOnlyList<string> Missing(Solution solution) =>
    [
        .. solution.Projects
            .Where(project => project.Language == LanguageNames.CSharp && !HasRun(project))
            .Select(project => project.Name)
            .Order(StringComparer.Ordinal)
    ];
}
