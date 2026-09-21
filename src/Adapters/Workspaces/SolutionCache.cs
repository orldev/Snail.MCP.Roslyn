using System.Collections.Concurrent;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Options;
using Snail.MCP.Roslyn.Application;
using Snail.MCP.Roslyn.Configuration;

namespace Snail.MCP.Roslyn.Adapters.Workspaces;

/// <summary>Holds loaded solutions and brings them level with the files on disk before every answer.</summary>
internal sealed class SolutionCache(IOptions<WorkspaceOptions> options, SessionReach reach) : IDisposable
{
    /// <summary>macOS and Windows both hand back paths whose case need not match MSBuild's.</summary>
    private static readonly StringComparer PathComparer =
        OperatingSystem.IsLinux() ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;

    private static readonly string[] SourcePatterns = ["*.cs", "*.razor", "*.cshtml"];

    private readonly ConcurrentDictionary<string, Entry> entries = new(StringComparer.Ordinal);

    /// <summary>Ids dropped to stay within the ceiling, so a call carrying one is told what happened to it
    /// rather than being told it never loaded the solution.</summary>
    private readonly ConcurrentDictionary<string, byte> dropped = new(StringComparer.Ordinal);

    public int Loaded => entries.Count;

    /// <summary>Whether this solution is still held, so a caller can reuse it instead of opening it again.</summary>
    public bool Holds(string id) => entries.ContainsKey(id);

    public async Task StoreAsync(
        string id,
        MSBuildWorkspace workspace,
        Solution solution,
        string entryPath,
        CancellationToken cancellationToken)
    {
        if (entries.TryRemove(id, out var previous))
            await previous.RetireAsync(cancellationToken);

        dropped.TryRemove(id, out _);
        entries[id] = Adopt(workspace, solution, entryPath);

        await EvictAsync(cancellationToken);
    }

    /// <summary>Drops the solutions used longest ago once there are more than the ceiling allows. Each one holds
    /// its own compilations, so an unbounded cache grows until the process is killed.</summary>
    private async Task EvictAsync(CancellationToken cancellationToken)
    {
        var ceiling = Math.Max(1, options.Value.MaxSolutions);

        while (entries.Count > ceiling)
        {
            var stale = entries.OrderBy(entry => entry.Value.LastUsed).First();

            if (!entries.TryRemove(stale.Key, out var evicted))
                continue;

            dropped[stale.Key] = 0;

            await evicted.RetireAsync(cancellationToken);
        }
    }

    /// <summary>The solution level with the files on disk. Callers may run concurrently, so the whole
    /// stale-check-and-refresh runs under the entry's gate: it mutates the entry's stamps and exclusions,
    /// and two callers racing there corrupt a Dictionary rather than merely losing an update.</summary>
    public async Task<Solution> FreshAsync(string id, CancellationToken cancellationToken)
    {
        if (!entries.TryGetValue(id, out var entry))
        {
            throw new InvalidOperationException(dropped.ContainsKey(id)
                ? $"Solution '{id}' was dropped to keep at most {Math.Max(1, options.Value.MaxSolutions)} solutions in memory. Call load_solution again."
                : $"Solution '{id}' is not loaded. Call load_solution first.");
        }

        if (!reach.Allows(entry.EntryPath))
            throw new InvalidOperationException(
                $"Solution '{id}' was loaded by another session. {reach.Refusal(entry.EntryPath)}");

        entry.LastUsed = DateTime.UtcNow;

        await entry.EnterAsync(cancellationToken);

        try
        {
            if (Restructured(entry))
                return await ReopenAsync(id, entry, cancellationToken);

            var solution = entry.Solution;

            foreach (var document in solution.Projects.SelectMany(project => project.Documents))
            {
                if (await ReadChangedAsync(entry, document, cancellationToken) is { } text)
                    solution = solution.WithDocumentText(document.Id, text);
            }

            foreach (var document in solution.Projects.SelectMany(project => project.AdditionalDocuments))
            {
                if (await ReadChangedAsync(entry, document, cancellationToken) is { } text)
                    solution = solution.WithAdditionalDocumentText(document.Id, text);
            }

            entry.Solution = solution;

            return solution;
        }
        finally
        {
            entry.Exit();
        }
    }

    private static Entry Adopt(MSBuildWorkspace workspace, Solution solution, string entryPath)
    {
        var entry = new Entry(workspace, solution, entryPath);

        // Whatever is on disk at open time and is still not a document is excluded by the project itself.
        entry.Excluded.UnionWith(OnDiskOnly(solution));

        return entry;
    }

    private static async Task<SourceText?> ReadChangedAsync(
        Entry entry,
        TextDocument document,
        CancellationToken cancellationToken)
    {
        if (document.FilePath is not { } path || !File.Exists(path))
            return null;

        var stamp = File.GetLastWriteTimeUtc(path);

        if (entry.Stamps.TryGetValue(document.Id, out var known) && known == stamp)
            return null;

        var text = SourceText.From(await File.ReadAllTextAsync(path, cancellationToken));

        // Stamped only once the read succeeded: a cancelled read must not mark the file as seen.
        entry.Stamps[document.Id] = stamp;

        return text;
    }

    public async Task<bool> RemoveAsync(string id, CancellationToken cancellationToken)
    {
        if (!entries.TryRemove(id, out var entry))
            return false;

        await entry.RetireAsync(cancellationToken);

        return true;
    }

    /// <summary>Shutdown, where nobody is left to wait for: the workspaces go directly, without their gates.</summary>
    public void Dispose()
    {
        foreach (var entry in entries.Values)
            entry.Dispose();

        entries.Clear();
    }

    /// <summary>Replaces the entry in place. The stale workspace is disposed directly rather than retired:
    /// this runs holding its gate, so no other caller can be inside it and waiting for the gate would deadlock.</summary>
    private async Task<Solution> ReopenAsync(string id, Entry stale, CancellationToken cancellationToken)
    {
        var workspace = MSBuildWorkspace.Create();

        var solution = Path.GetExtension(stale.EntryPath) is ".sln" or ".slnx" or ".slnf"
            ? await workspace.OpenSolutionAsync(stale.EntryPath, cancellationToken: cancellationToken)
            : (await workspace.OpenProjectAsync(stale.EntryPath, cancellationToken: cancellationToken)).Solution;

        entries[id] = Adopt(workspace, solution, stale.EntryPath);
        stale.Dispose();

        return solution;
    }

    /// <summary>True when a file the solution should contain appeared or vanished, which text replacement alone
    /// cannot absorb.</summary>
    /// <remarks>A file the project excludes — a Compile Remove, a nested project outside the solution, a scratch
    /// file — sits on disk forever and is not a document, so a plain "on disk but not a document" test reopens the
    /// whole solution through MSBuild on every single call. Once a reopen has declined to adopt a path, it is
    /// recorded as excluded and never asked about again, until a project file changes and the answer may differ.</remarks>
    private static bool Restructured(Entry entry)
    {
        if (ProjectFilesChanged(entry))
            return true;

        var known = Contained(entry.Solution);

        foreach (var candidate in SourceFilesUnder(entry.Solution))
        {
            if (!known.Contains(candidate) && !entry.Excluded.Contains(candidate))
                return true;
        }

        return known.Any(path => !File.Exists(path));
    }

    /// <summary>A changed project file can add or drop source files wholesale, so the exclusions it justified
    /// no longer hold and the workspace has to be built again.</summary>
    private static bool ProjectFilesChanged(Entry entry)
    {
        var changed = false;

        foreach (var project in entry.Solution.Projects)
        {
            if (project.FilePath is not { } file || !File.Exists(file))
                continue;

            var stamp = File.GetLastWriteTimeUtc(file);

            if (entry.ProjectStamps.TryGetValue(file, out var known) && known == stamp)
                continue;

            entry.ProjectStamps[file] = stamp;
            changed |= known != default;
        }

        return changed;
    }

    private static IEnumerable<string> OnDiskOnly(Solution solution)
    {
        var known = Contained(solution);

        return SourceFilesUnder(solution).Where(candidate => !known.Contains(candidate));
    }

    private static HashSet<string> Contained(Solution solution) =>
    [
        .. solution.Projects.SelectMany(project => project.Documents).Select(document => document.FilePath).OfType<string>(),
        .. solution.Projects.SelectMany(project => project.AdditionalDocuments)
            .Select(document => document.FilePath)
            .Where(MarkupMap.IsMarkup)
            .OfType<string>()
    ];

    /// <summary>Source files under every project root, each root visited once however many target frameworks
    /// share it, and without descending into build output.</summary>
    private static IEnumerable<string> SourceFilesUnder(Solution solution)
    {
        HashSet<string> roots = new(PathComparer);

        foreach (var project in solution.Projects)
        {
            if (project.FilePath is { } file && Path.GetDirectoryName(file) is { } root)
                roots.Add(root);
        }

        return roots.SelectMany(root => FileTree.Files(root, SourcePatterns));
    }

    private sealed class Entry(MSBuildWorkspace workspace, Solution solution, string entryPath) : IDisposable
    {
        private readonly SemaphoreSlim gate = new(1, 1);

        public Solution Solution { get; set; } = solution;

        public DateTime LastUsed { get; set; } = DateTime.UtcNow;

        public string EntryPath { get; } = entryPath;

        public Dictionary<DocumentId, DateTime> Stamps { get; } = Snapshot(solution);

        /// <summary>Paths a reopen refused to adopt, so they stop being a reason to reopen.</summary>
        public HashSet<string> Excluded { get; } = new(PathComparer);

        public Dictionary<string, DateTime> ProjectStamps { get; } = new(PathComparer);

        public Task EnterAsync(CancellationToken cancellationToken) => gate.WaitAsync(cancellationToken);

        public void Exit() => gate.Release();

        /// <summary>Releases the workspace once whoever is inside the entry has left, so an unload or a reload
        /// cannot pull the compilations out from under a request already reading them.</summary>
        /// <remarks>Awaited rather than waited on: this runs while loading a solution and while evicting one, and
        /// blocking a request thread until an unrelated request finishes is how a server runs out of them.</remarks>
        public async Task RetireAsync(CancellationToken cancellationToken)
        {
            await gate.WaitAsync(cancellationToken);

            try
            {
                workspace.Dispose();
            }
            finally
            {
                gate.Release();
            }
        }

        public void Dispose() => workspace.Dispose();

        private static Dictionary<DocumentId, DateTime> Snapshot(Solution solution)
        {
            Dictionary<DocumentId, DateTime> stamps = [];

            IEnumerable<TextDocument> tracked =
            [
                .. solution.Projects.SelectMany(project => project.Documents),
                .. solution.Projects.SelectMany(project => project.AdditionalDocuments)
            ];

            foreach (var document in tracked)
            {
                if (document.FilePath is { } path && File.Exists(path))
                    stamps[document.Id] = File.GetLastWriteTimeUtc(path);
            }

            return stamps;
        }
    }
}
