using Microsoft.CodeAnalysis;

namespace Snail.MCP.Roslyn.Adapters.Workspaces;

/// <summary>Tells whether package restore has run, by the file it leaves behind rather than by a proxy.</summary>
/// <remarks>
/// The file is the evidence, but it lands in one of two places and only one of them used to be looked at.
/// A repository that turns on the artifacts layout — <c>UseArtifactsOutput</c>, in .NET since 8, which
/// Snail.Sdk's own libraries set — keeps it at <c>artifacts/obj/{Project}/project.assets.json</c> rather
/// than beside the project. Measured 2026-10-15 on Snail.Toolkit.Web: sixteen projects, each resolving
/// between 176 and 219 references, every one of them called unrestored. The cost was not only a wrong
/// note: this server restores what it believes unrestored, so it spent 687 ms running
/// <c>dotnet restore</c> on a solution that needed none, and told the agent symbols do not bind while
/// they did.
/// Resolved references alone were tried as the signal and rejected: an unrestored project whose only
/// dependency is the framework still resolves its reference pack from the SDK, so the count says nothing
/// about packages.
/// </remarks>
internal static class ProjectRestore
{
    private const string Assets = "project.assets.json";

    private const string Artifacts = "artifacts";

    public static bool HasRun(Project project)
    {
        if (project.FilePath is not { } file || Path.GetDirectoryName(file) is not { } folder)
            return true;

        if (File.Exists(Path.Combine(folder, "obj", Assets)))
            return true;

        return IsUnderArtifacts(folder, Path.GetFileNameWithoutExtension(file));
    }

    /// <summary>
    /// The artifacts layout keeps every project's intermediate output under one folder of the repository,
    /// named after the project file. Which ancestor holds it is a property of the repository rather than
    /// of the project, so it is looked for upwards until the root — a handful of checks for a file.
    /// </summary>
    private static bool IsUnderArtifacts(string folder, string name)
    {
        for (var current = new DirectoryInfo(folder); current is not null; current = current.Parent)
            if (File.Exists(Path.Combine(current.FullName, Artifacts, "obj", name, Assets)))
                return true;

        return false;
    }

    public static IReadOnlyList<string> Missing(Solution solution) =>
    [
        .. solution.Projects
            .Where(project => project.Language == LanguageNames.CSharp && !HasRun(project))
            .Select(project => project.Name)
            .Order(StringComparer.Ordinal)
    ];
}
