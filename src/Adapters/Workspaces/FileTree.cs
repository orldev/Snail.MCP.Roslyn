namespace Snail.MCP.Roslyn.Adapters.Workspaces;

/// <summary>Walks a project tree without descending into what the build and the tools own.</summary>
/// <remarks>Skipping is done during the walk, not after it: filtering the result of a full enumeration still
/// pays for every file under obj, bin and node_modules, which on a repository with a front end is most of them.</remarks>
internal static class FileTree
{
    private static readonly string[] Skipped = ["obj", "bin", ".git", ".vs", "node_modules"];

    private static readonly EnumerationOptions Shallow = new() { IgnoreInaccessible = true };

    public static IEnumerable<string> Files(string root, params string[] patterns)
    {
        Stack<string> pending = new();
        pending.Push(root);

        while (pending.Count > 0)
        {
            var folder = pending.Pop();

            foreach (var child in Directory.EnumerateDirectories(folder, "*", Shallow))
            {
                if (!Skipped.Contains(Path.GetFileName(child), StringComparer.OrdinalIgnoreCase))
                    pending.Push(child);
            }

            foreach (var pattern in patterns)
            {
                foreach (var file in Directory.EnumerateFiles(folder, pattern, Shallow))
                    yield return file;
            }
        }
    }
}
