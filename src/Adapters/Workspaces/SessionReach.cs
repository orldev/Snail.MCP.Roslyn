using Snail.Sdk.Mcp.Hosting.Sessions;

namespace Snail.MCP.Roslyn.Adapters.Workspaces;

/// <summary>Whether this session may open a solution at a path.</summary>
/// <remarks>
/// Snail.Sdk's roots decide, with one exception they do not make: a session that named no root at a server configured
/// with none reaches wherever the process can. That is a server started as a program for one caller — the shape a
/// desktop client installs, with no settings at all — and it has always opened whatever solution its caller named. The
/// SDK's <see cref="SessionRoots"/> reaches nothing there, and a volume of <c>/</c> holds nothing either (its prefix is
/// <c>//</c>), so the exception lives here until <see cref="SessionRootsOptions"/> can say it.
/// Over HTTP the door refuses such a session before a call is made.
/// </remarks>
internal static class SessionReach
{
    extension(SessionRoots roots)
    {
        /// <summary>The path as the session may open it, or why it may not.</summary>
        public Reached Opens(string path) =>
            roots.Named.Count == 0 && roots.All.Count == 0 ? Reached.Yes(path) : roots.Reach(path);
    }

    extension(Reached reached)
    {
        /// <summary>The refusal with what to do about it, as one sentence for a note or a message.</summary>
        public string Told => reached.Hint is { } hint ? $"{reached.Refusal}: {hint}" : reached.Refusal ?? string.Empty;
    }
}
