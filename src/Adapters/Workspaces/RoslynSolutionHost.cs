using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;

namespace Snail.MCP.Roslyn.Adapters.Workspaces;

internal sealed class RoslynSolutionHost(IPathMap paths, SolutionCache cache, ILogger<RoslynSolutionHost> log)
    : ISolutionHost
{
    private const string RestoreHint =
        "Package restore has not run for at least one project: no obj/project.assets.json exists. "
        + "Run 'dotnet restore' on the solution first. Until then any error about a missing type or namespace "
        + "is about the missing packages, not about the code — do not 'fix' code that is not broken. "
        + "Semantic answers also under-report, so an empty result does not mean the code is clean.";

    private const string CompilationHint =
        "At least one project produced no compilation, so every semantic answer would be empty regardless of the code. "
        + "This is a server fault, not a property of the analysed project.";

    private const string UnloadedHint =
        "Some .csproj files under this directory are not part of the loaded solution and were not analysed. "
        + "Answers cover the solution only: absence of a result there does not mean absence in the directory.";

    public async Task<WorkspaceReport> LoadAsync(string path, CancellationToken cancellationToken)
    {
        var target = paths.ToProcess(path);
        var entry = Resolve(target);

        if (entry is null)
            return Unopened(target, $"No solution or project found at {paths.ToCaller(target)}");

        var started = Stopwatch.GetTimestamp();
        var notes = new ConcurrentBag<string>();
        var workspace = MSBuildWorkspace.Create();
        using var listener = workspace.RegisterWorkspaceFailedHandler(failure => notes.Add(failure.Diagnostic.Message));

        Solution solution;

        try
        {
            solution = await OpenAsync(workspace, entry, cancellationToken);
        }
        catch (Exception error)
        {
            workspace.Dispose();
            log.LogError(error, "Failed to open {Entry}", entry);

            return Unopened(target, error.Message);
        }

        var projects = Inventory(solution);
        var unloaded = Unloaded(entry, solution);
        var (errors, warnings, compiled) = await CountAsync(solution, cancellationToken);
        var unrestored = ProjectRestore.Missing(solution);
        var readiness = Decide(projects, compiled, unrestored.Count == 0);

        cache.Store(Identify(entry), workspace, solution, entry);

        return new WorkspaceReport(
            Identify(entry),
            readiness,
            paths.ToCaller(entry),
            projects,
            unloaded,
            errors,
            warnings,
            (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds,
            Summarize(notes, readiness, unloaded.Count, unrestored));
    }

    public UnloadReport Unload(string solutionId)
    {
        var removed = cache.Remove(solutionId);

        return new UnloadReport(
            solutionId,
            removed,
            removed
                ? ["The solution and its compilations are released. Load it again before asking anything else about it."]
                : ["No solution with this id is loaded; nothing was held and nothing was released."]);
    }

    private static string? Resolve(string target)
    {
        if (File.Exists(target))
            return target;

        if (!Directory.Exists(target))
            return null;

        string[] patterns = ["*.slnx", "*.sln", "*.csproj"];

        foreach (var pattern in patterns)
        {
            var found = Directory.GetFiles(target, pattern, SearchOption.TopDirectoryOnly);

            if (found.Length > 0)
                return found[0];
        }

        return null;
    }

    private static async Task<Solution> OpenAsync(MSBuildWorkspace workspace, string entry, CancellationToken cancellationToken)
    {
        if (Path.GetExtension(entry) is ".sln" or ".slnx" or ".slnf")
            return await workspace.OpenSolutionAsync(entry, cancellationToken: cancellationToken);

        var project = await workspace.OpenProjectAsync(entry, cancellationToken: cancellationToken);

        return project.Solution;
    }

    private static IReadOnlyList<LoadedProject> Inventory(Solution solution) =>
    [
        .. solution.Projects
            .Where(IsCSharp)
            .Select(project => new LoadedProject(
                project.Name,
                TargetOf(project),
                project.DocumentIds.Count,
                project.MetadataReferences.Count,
                project.MetadataReferences.Count > 0))
            .OrderBy(project => project.Name, StringComparer.Ordinal)
    ];

    private static async Task<(int Errors, int Warnings, bool Compiled)> CountAsync(
        Solution solution,
        CancellationToken cancellationToken)
    {
        var errors = 0;
        var warnings = 0;
        var compiled = true;

        foreach (var project in solution.Projects.Where(IsCSharp))
        {
            var compilation = await project.GetCompilationAsync(cancellationToken);

            if (compilation is null)
            {
                compiled = false;

                continue;
            }

            foreach (var diagnostic in compilation.GetDiagnostics(cancellationToken))
            {
                if (diagnostic.Severity == DiagnosticSeverity.Error)
                    errors++;

                if (diagnostic.Severity == DiagnosticSeverity.Warning)
                    warnings++;
            }
        }

        return (errors, warnings, compiled);
    }

    private static WorkspaceReadiness Decide(IReadOnlyList<LoadedProject> projects, bool compiled, bool restored)
    {
        if (projects.Count == 0 || !compiled)
            return WorkspaceReadiness.Failed;

        return restored && projects.All(project => project.HasSemantics)
            ? WorkspaceReadiness.Ready
            : WorkspaceReadiness.Degraded;
    }

    private static IReadOnlyList<string> Summarize(
        ConcurrentBag<string> notes,
        WorkspaceReadiness readiness,
        int unloaded,
        IReadOnlyList<string> unrestored)
    {
        List<string> summary = [.. notes.Distinct(StringComparer.Ordinal).Take(10)];

        if (readiness == WorkspaceReadiness.Failed)
            summary.Insert(0, CompilationHint);

        if (unrestored.Count > 0)
            summary.Insert(0, $"{RestoreHint} Not restored: {string.Join(", ", unrestored.Take(10))}.");
        else if (readiness == WorkspaceReadiness.Degraded)
            summary.Insert(0, RestoreHint);

        if (unloaded > 0)
            summary.Insert(0, $"{UnloadedHint} Not analysed: {unloaded}.");

        return summary;
    }

    private static string Identify(string entry) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(entry)))[..8];

    private static bool IsCSharp(Project project) => project.Language == LanguageNames.CSharp;

    private static string? TargetOf(Project project)
    {
        var open = project.Name.IndexOf('(');

        return open < 0 ? null : project.Name[(open + 1)..].TrimEnd(')');
    }

    private IReadOnlyList<string> Unloaded(string entry, Solution solution)
    {
        var root = Path.GetDirectoryName(entry);

        if (root is null)
            return [];

        HashSet<string> known = [.. solution.Projects.Select(project => project.FilePath).OfType<string>()];

        return
        [
            .. Directory.EnumerateFiles(root, "*.csproj", SearchOption.AllDirectories)
                .Where(file => !known.Contains(file) && !SourcePaths.IsUnderOutput(file))
                .Select(paths.ToCaller)
                .Order(StringComparer.Ordinal)
        ];
    }

    private WorkspaceReport Unopened(string target, string note) =>
        new(string.Empty, WorkspaceReadiness.Failed, paths.ToCaller(target), [], [], 0, 0, 0, [note]);
}
