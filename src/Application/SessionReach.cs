using Microsoft.Extensions.Options;
using Snail.MCP.Roslyn.Configuration;

namespace Snail.MCP.Roslyn.Application;

/// <summary>How far the session in front of us may reach into the filesystem.</summary>
/// <remarks>
/// One process used to serve one caller, so the question never came up: whatever solution was asked
/// for was the caller's own. A container shared by the workers of a machine is a different animal —
/// every one of them has its own working copy on the same disk, and the whole point is that none of
/// them reaches another's.
/// <para>
/// The reach is read from the address the session was opened at — <c>…/mcp?root=/workspaces/task</c> —
/// and not from anything the call itself carries, because a call can say whatever it likes. A session
/// that named no root reaches everywhere, which is exactly right for a server started as a program for
/// one caller and would be exactly wrong for a shared one; that is a property of how it was deployed,
/// and the deployment states it by opening sessions with a root or not.
/// </para>
/// </remarks>
public sealed class SessionReach(IOptions<WorkspaceOptions> options, ISessionAddress address)
{
    /// <summary>What the address calls the root it hands over; repeat it for more than one.</summary>
    public const string Parameter = "root";

    /// <summary>Whether this session may touch that path at all.</summary>
    public bool Allows(string path)
    {
        var roots = Roots;

        return roots.Count == 0 || roots.Any(root => IsUnder(path, root));
    }

    /// <summary>The refusal, in the words the caller needs to fix the call.</summary>
    public string Refusal(string path) =>
        $"'{path}' is outside this session, which works in {string.Join(", ", Roots)}";

    /// <summary>Of the roots a session names, the ones this machine does not have.</summary>
    /// <remarks>
    /// Asked before a session is let in, so that a worker pointed at the server of another machine hears
    /// about it at once. A root that does not exist here is never an empty result to be worked around: it
    /// means the copy is on a different host, and every answer this session could give would be about the
    /// wrong solution.
    /// </remarks>
    public static IReadOnlyList<string> Absent(IEnumerable<string> named) =>
    [
        .. Paths(named).Where(root => !Directory.Exists(root))
    ];

    /// <summary>The roots of this session: the ones its address names, or the configured ones when it names none.</summary>
    /// <remarks>
    /// Either, never both. A shared server is configured with the whole volume of working copies so that it
    /// can serve any of them — adding that to what the session named would hand every session every other
    /// task's code, which is the isolation this exists to keep. Empty means «wherever the process can reach»,
    /// which is right for a server started as a program and would be wrong for a shared one.
    /// </remarks>
    private IReadOnlyList<string> Roots =>
        Paths(address.Values(Parameter)) is { Count: > 0 } named ? named : Paths(options.Value.SessionRoots);

    private static IReadOnlyList<string> Paths(IEnumerable<string> roots) =>
    [
        .. roots
            .Where(root => !string.IsNullOrWhiteSpace(root))
            .Select(root => Path.GetFullPath(root.Trim()))
            .Distinct(StringComparer.Ordinal)
    ];

    /// <summary>
    /// True when the path is the root itself or lies inside it, compared after links are resolved.
    /// </summary>
    /// <remarks>
    /// Links are resolved on both sides because a symlink inside a root is the cheapest way out of it,
    /// and a bare prefix test would let a sibling whose name merely begins the same way — <c>/w/task-1-x</c>
    /// next to <c>/w/task-1</c> — pass as a path inside.
    /// </remarks>
    private static bool IsUnder(string path, string root)
    {
        var full = Real(Path.GetFullPath(path));
        var inside = Real(root);

        return string.Equals(full, inside, StringComparison.Ordinal)
               || full.StartsWith(inside.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.Ordinal);
    }

    /// <summary>The path with every link on it resolved; the nearest existing parent for one not created yet.</summary>
    private static string Real(string path)
    {
        var existing = path;
        var tail = new Stack<string>();

        while (!Directory.Exists(existing) && !File.Exists(existing))
        {
            var parent = Path.GetDirectoryName(existing);
            if (string.IsNullOrEmpty(parent) || parent == existing)
                return path;

            tail.Push(Path.GetFileName(existing));
            existing = parent;
        }

        var resolved = Resolved(existing);

        return tail.Count == 0 ? resolved : Path.Combine([resolved, .. tail]);
    }

    /// <summary>An existing path with every link along it followed to where it really goes.</summary>
    /// <remarks>
    /// Every link, not just the last one: <c>ResolveLinkTarget</c> answers about the final component
    /// alone, so a link in the middle of a path resolves to itself and walks straight out of the root.
    /// </remarks>
    private static string Resolved(string existing)
    {
        if (Link(existing) is { } target)
            return Resolved(target);

        var parent = Path.GetDirectoryName(existing);

        if (string.IsNullOrEmpty(parent) || parent == existing)
            return existing;

        var real = Resolved(parent);

        return string.Equals(real, parent, StringComparison.Ordinal)
            ? existing
            : Path.Combine(real, Path.GetFileName(existing));
    }

    private static string? Link(string path) =>
        (Directory.Exists(path)
            ? new DirectoryInfo(path).ResolveLinkTarget(returnFinalTarget: true)
            : new FileInfo(path).ResolveLinkTarget(returnFinalTarget: true))?.FullName;
}
