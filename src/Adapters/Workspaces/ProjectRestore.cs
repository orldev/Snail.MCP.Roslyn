using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;

namespace Snail.MCP.Roslyn.Adapters.Workspaces;

/// <summary>Tells whether package restore has run for the packages a project declares now, by the file it leaves behind rather than by a proxy.</summary>
/// <remarks>
/// The file is the evidence, but it lands in one of two places and only one of them used to be looked at.
/// A repository that turns on the artifacts layout — <c>UseArtifactsOutput</c>, in .NET since 8, which
/// Snail.Sdk's own libraries set — keeps it at <c>artifacts/obj/{Project}/project.assets.json</c> rather
/// than beside the project. Measured 2026-10-15 on Snail.Toolkit.Web: sixteen projects, each resolving
/// between 176 and 219 references, every one of them called unrestored. The cost was not only a wrong
/// note: this server restores what it believes unrestored, so it spent 687 ms running
/// <c>dotnet restore</c> on a solution that needed none, and told the agent symbols do not bind while
/// they did.
/// <para>
/// A file that exists may still be older than what the project declares: a checkout of a branch that adds a
/// package leaves the previous branch's assets behind, the package's references are missing, and the solution
/// was called Ready over a flood of CS0234 and CS0246 about code that was fine (2026-10-10, 313 references where
/// a build made 339). The SDK says nothing of it either: a build without restore fails with CS0103 alone.
/// Timestamps alone cannot tell: a restore that changes nothing rewrites none of its files (measured
/// 2026-11-09), so a project file newer than its assets is what every edit leaves. They only decide whether to
/// look: when the project or a file it inherits packages from changed after the restore, every package they
/// name must be among those the assets hold.
/// </para>
/// Resolved references alone were tried as the signal and rejected: an unrestored project whose only
/// dependency is the framework still resolves its reference pack from the SDK, so the count says nothing
/// about packages.
/// </remarks>
internal static class ProjectRestore
{
    private const string Assets = "project.assets.json";

    private const string Artifacts = "artifacts";

    /// <summary>The files beside or above a project that may declare its packages; MSBuild imports the nearest of each.</summary>
    private static readonly string[] Inherited = ["Directory.Build.props", "Directory.Build.targets", "Directory.Packages.props"];

    private static readonly string[] Declarations = ["PackageReference", "GlobalPackageReference"];

    public static bool HasRun(Project project) =>
        project.FilePath is not { } file || HasRun(file);

    /// <summary>Whether the packages of the project at <paramref name="projectFile"/> are restored as it declares them now.</summary>
    public static bool HasRun(string projectFile)
    {
        if (Path.GetDirectoryName(projectFile) is not { } folder)
            return true;

        return Find(folder, Path.GetFileNameWithoutExtension(projectFile)) is { } assets && IsCurrent(projectFile, assets);
    }

    public static IReadOnlyList<string> Missing(Solution solution) =>
    [
        .. solution.Projects
            .Where(project => project.Language == LanguageNames.CSharp && !HasRun(project))
            .Select(project => project.Name)
            .Order(StringComparer.Ordinal)
    ];

    /// <summary>
    /// The assets file beside the project, or under the artifacts folder that keeps every project's intermediate output,
    /// named after the project file. Which ancestor holds that folder is a property of the repository rather than of the
    /// project, so it is looked for upwards until the root — a handful of checks for a file.
    /// </summary>
    private static FileInfo? Find(string folder, string name)
    {
        var beside = new FileInfo(Path.Combine(folder, "obj", Assets));

        if (beside.Exists)
            return beside;

        for (var current = new DirectoryInfo(folder); current is not null; current = current.Parent)
        {
            var shared = new FileInfo(Path.Combine(current.FullName, Artifacts, "obj", name, Assets));

            if (shared.Exists)
                return shared;
        }

        return null;
    }

    private static bool IsCurrent(string projectFile, FileInfo assets)
    {
        var changed = Declaring(projectFile).Where(file => file.LastWriteTimeUtc > assets.LastWriteTimeUtc).ToList();

        if (changed.Count == 0)
            return true;

        var restored = Restored(assets);

        return changed.SelectMany(Declared).All(restored.Contains);
    }

    private static IEnumerable<FileInfo> Declaring(string projectFile)
    {
        var project = new FileInfo(projectFile);

        yield return project;

        foreach (var name in Inherited)
            if (Nearest(project.Directory, name) is { } inherited)
                yield return inherited;
    }

    private static FileInfo? Nearest(DirectoryInfo? folder, string name)
    {
        for (var current = folder; current is not null; current = current.Parent)
        {
            var file = new FileInfo(Path.Combine(current.FullName, name));

            if (file.Exists)
                return file;
        }

        return null;
    }

    /// <summary>
    /// The packages a file names unconditionally. A reference under a condition, or named through a property, may not
    /// apply at all, and counting it would call the project unrestored after every restore; it is left out, and a
    /// change to it goes unseen.
    /// </summary>
    private static IEnumerable<string> Declared(FileInfo file)
    {
        XDocument document;

        try
        {
            document = XDocument.Load(file.FullName);
        }
        catch (Exception error) when (error is XmlException or IOException or UnauthorizedAccessException)
        {
            return [];
        }

        return document.Descendants()
            .Where(element => Declarations.Contains(element.Name.LocalName) && !IsConditional(element))
            .Select(element => element.Attribute("Include")?.Value ?? string.Empty)
            .Where(include => !include.Contains("$(", StringComparison.Ordinal))
            .SelectMany(include => include.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private static bool IsConditional(XElement element) =>
        element.AncestorsAndSelf().Any(node => node.Attribute("Condition") is not null);

    /// <summary>The packages the assets file was restored for, in any framework; none when it cannot be read.</summary>
    private static HashSet<string> Restored(FileInfo assets)
    {
        var restored = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            using var stream = assets.OpenRead();
            using var document = JsonDocument.Parse(stream);

            if (!document.RootElement.TryGetProperty("project", out var project) || !project.TryGetProperty("frameworks", out var frameworks))
                return restored;

            foreach (var framework in frameworks.EnumerateObject())
                if (framework.Value.TryGetProperty("dependencies", out var dependencies))
                    foreach (var dependency in dependencies.EnumerateObject())
                        restored.Add(dependency.Name);
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            restored.Clear();
        }

        return restored;
    }
}
