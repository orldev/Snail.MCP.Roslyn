using System.Collections.Concurrent;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.CodeAnalysis.Text;

namespace Snail.MCP.Roslyn.Adapters.Workspaces;

/// <summary>Holds loaded solutions and brings them level with the files on disk before every answer.</summary>
internal sealed class SolutionCache : IDisposable
{
    private readonly ConcurrentDictionary<string, Entry> entries = new(StringComparer.Ordinal);

    public void Store(string id, MSBuildWorkspace workspace, Solution solution, string entryPath)
    {
        if (entries.TryRemove(id, out var previous))
            previous.Dispose();

        entries[id] = new Entry(workspace, solution, entryPath);
    }

    public async Task<Solution> FreshAsync(string id, CancellationToken cancellationToken)
    {
        if (!entries.TryGetValue(id, out var entry))
            throw new InvalidOperationException($"Solution '{id}' is not loaded. Call load_solution first.");

        if (Restructured(entry.Solution))
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

        entry.Stamps[document.Id] = stamp;

        return SourceText.From(await File.ReadAllTextAsync(path, cancellationToken));
    }

    public bool Remove(string id)
    {
        if (!entries.TryRemove(id, out var entry))
            return false;

        entry.Dispose();

        return true;
    }

    public void Dispose()
    {
        foreach (var entry in entries.Values)
            entry.Dispose();

        entries.Clear();
    }

    private async Task<Solution> ReopenAsync(string id, Entry stale, CancellationToken cancellationToken)
    {
        var workspace = MSBuildWorkspace.Create();

        var solution = Path.GetExtension(stale.EntryPath) is ".sln" or ".slnx" or ".slnf"
            ? await workspace.OpenSolutionAsync(stale.EntryPath, cancellationToken: cancellationToken)
            : (await workspace.OpenProjectAsync(stale.EntryPath, cancellationToken: cancellationToken)).Solution;

        Store(id, workspace, solution, stale.EntryPath);

        return solution;
    }

    /// <summary>True when a source or markup file appeared or vanished, which text replacement alone cannot absorb.</summary>
    private static bool Restructured(Solution solution)
    {
        HashSet<string> known =
        [
            .. solution.Projects.SelectMany(project => project.Documents).Select(document => document.FilePath).OfType<string>(),
            .. solution.Projects.SelectMany(project => project.AdditionalDocuments)
                .Select(document => document.FilePath)
                .Where(MarkupMap.IsMarkup)
                .OfType<string>()
        ];

        foreach (var project in solution.Projects)
        {
            if (project.FilePath is not { } file || Path.GetDirectoryName(file) is not { } root)
                continue;

            foreach (var pattern in (string[])["*.cs", "*.razor", "*.cshtml"])
            {
                foreach (var candidate in Directory.EnumerateFiles(root, pattern, SearchOption.AllDirectories))
                {
                    if (!SourcePaths.IsUnderOutput(candidate) && !known.Contains(candidate))
                        return true;
                }
            }
        }

        return known.Any(path => !File.Exists(path));
    }

    private sealed class Entry(MSBuildWorkspace workspace, Solution solution, string entryPath) : IDisposable
    {
        public Solution Solution { get; set; } = solution;

        public string EntryPath { get; } = entryPath;

        public Dictionary<DocumentId, DateTime> Stamps { get; } = Snapshot(solution);

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
