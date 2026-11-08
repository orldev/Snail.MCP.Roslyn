using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using Snail.Sdk.Mcp.Hosting.Answers;
using Snail.Sdk.Mcp.Hosting.Sessions;

namespace Snail.MCP.Roslyn.Adapters.Workspaces;

internal sealed class RoslynSolutionHost(
    IPathMap paths,
    SolutionCache cache,
    SessionRoots reach,
    PackageRestore restore,
    ILogger<RoslynSolutionHost> log)
    : ISolutionHost
{
    private const string NotFoundHint = "Name a .sln, .slnx or .csproj file, or the folder that holds one.";

    private const string UnopenedHint =
        "Its message says why; 'dotnet build' on the same path shows the same failure in full. Nothing was loaded.";

    private const string ReusedHint =
        "This solution was already loaded and was reused: its compilations are the ones built earlier, "
        + "brought level with the files on disk. Call unload_solution first if you need it opened from scratch.";

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

    public async Task<WorkspaceReport> LoadAsync(
        string path,
        CancellationToken cancellationToken,
        bool countDiagnostics = false,
        IProgress<WorkStep>? progress = null)
    {
        var target = paths.ToProcess(path);

        if (reach.Reach(target) is { IsAllowed: false } refused)
            throw new ToolRefusalException($"Nothing was loaded: {refused.Refusal}", refused.Hint, refused.Type);

        var entry = Resolve(target)
            ?? throw new ToolRefusalException(
                $"No solution or project found at {paths.ToCaller(target)}",
                NotFoundHint,
                RefusalTypes.NotFound);

        var started = Stopwatch.GetTimestamp();
        var identity = Identify(entry);

        if (cache.Holds(identity))
            return await ReusedAsync(identity, entry, started, countDiagnostics, cancellationToken);

        var notes = new ConcurrentBag<string>();

        if (await restore.EnsureAsync(entry, cancellationToken) is { } restored)
            notes.Add(restored);

        var workspace = MSBuildWorkspace.Create();
        using var listener = workspace.RegisterWorkspaceFailedHandler(failure => notes.Add(failure.Diagnostic.Message));

        Solution solution;

        try
        {
            solution = await OpenAsync(workspace, entry, progress, cancellationToken);
        }
        catch (Exception error)
        {
            workspace.Dispose();
            Log.OpenFailed(log, error, entry);

            throw new ToolRefusalException(
                $"MSBuild could not open {paths.ToCaller(entry)}: {error.Message}",
                UnopenedHint,
                RefusalTypes.Unopened,
                error);
        }

        var projects = Inventory(solution);
        var unloaded = Unloaded(entry, solution);
        var (errors, warnings, compiled) = await CountAsync(solution, countDiagnostics, cancellationToken);
        var unrestored = ProjectRestore.Missing(solution);
        var readiness = Decide(projects, compiled, unrestored.Count == 0);

        await cache.StoreAsync(identity, workspace, solution, entry, cancellationToken);

        return new WorkspaceReport(
            identity,
            readiness,
            paths.ToCaller(entry),
            projects,
            unloaded,
            errors,
            warnings,
            (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds,
            Summarize(notes, readiness, unloaded.Count, unrestored));
    }

    /// <summary>The solution already in memory, brought level with the disk instead of opened again.</summary>
    /// <remarks>
    /// This is what a server that outlives a step is for. Opening a solution is the expensive part —
    /// seconds on a small repository, minutes on a large one — and a process that lived for one step had
    /// to pay it every time. The freshness is the same one every other tool call already relies on: the
    /// cache re-reads what changed, and reopens by itself when projects were added or removed.
    /// </remarks>
    private async Task<WorkspaceReport> ReusedAsync(
        string identity,
        string entry,
        long started,
        bool countDiagnostics,
        CancellationToken cancellationToken)
    {
        var solution = await cache.FreshAsync(identity, cancellationToken);
        var projects = Inventory(solution);
        var unloaded = Unloaded(entry, solution);
        var (errors, warnings, compiled) = await CountAsync(solution, countDiagnostics, cancellationToken);
        var unrestored = ProjectRestore.Missing(solution);
        var readiness = Decide(projects, compiled, unrestored.Count == 0);

        Log.Reused(log, entry, identity);

        return new WorkspaceReport(
            identity,
            readiness,
            paths.ToCaller(entry),
            projects,
            unloaded,
            errors,
            warnings,
            (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds,
            [ReusedHint, .. Summarize([], readiness, unloaded.Count, unrestored)]);
    }

    public async Task<UnloadReport> UnloadAsync(string solutionId, CancellationToken cancellationToken)
    {
        var removed = await cache.TryRemoveAsync(solutionId, cancellationToken);

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

    /// <summary>Opens the solution, telling the caller which project is being read as it goes.</summary>
    /// <remarks>MSBuild reports each project three times, once per phase; only the last is passed on, so the
    /// count is projects finished rather than phases run. How many there will be is not known until the
    /// solution file has been read, so no total is claimed.</remarks>
    private static async Task<Solution> OpenAsync(
        MSBuildWorkspace workspace,
        string entry,
        IProgress<WorkStep>? progress,
        CancellationToken cancellationToken)
    {
        var opened = 0;

        var reader = progress is null
            ? null
            : new Progress<ProjectLoadProgress>(loaded =>
            {
                if (loaded.Operation == ProjectLoadOperation.Resolve)
                    progress.Report(new WorkStep(++opened, null, $"Read {Path.GetFileName(loaded.FilePath)}"));
            });

        if (Path.GetExtension(entry) is ".sln" or ".slnx" or ".slnf")
            return await workspace.OpenSolutionAsync(entry, reader, cancellationToken);

        var project = await workspace.OpenProjectAsync(entry, reader, cancellationToken);

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

    /// <summary>Whether every project compiled, and the diagnostic counts only when they were asked for.</summary>
    /// <remarks>Counting means binding every method body of every project, which is the cost of a build, for
    /// two numbers that are stale as soon as the caller edits anything. get_diagnostics answers the same
    /// question against the files as they stand, so loading no longer pays for it.</remarks>
    private static async Task<(int? Errors, int? Warnings, bool Compiled)> CountAsync(
        Solution solution,
        bool counting,
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

            if (!counting)
                continue;

            foreach (var diagnostic in compilation.GetDiagnostics(cancellationToken))
            {
                if (diagnostic.Severity == DiagnosticSeverity.Error)
                    errors++;

                if (diagnostic.Severity == DiagnosticSeverity.Warning)
                    warnings++;
            }
        }

        return counting ? (errors, warnings, compiled) : (null, null, compiled);
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
            .. FileTree.Files(root, "*.csproj")
                .Where(file => !known.Contains(file))
                .Select(paths.ToCaller)
                .Order(StringComparer.Ordinal)
        ];
    }
}
