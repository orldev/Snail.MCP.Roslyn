namespace Snail.MCP.Roslyn.Adapters.Workspaces;

/// <summary>The kinds of the refusals this server answers for a caller's mistake, the <c>type</c> of the failure envelope.</summary>
/// <remarks>
/// Words a program can branch on, beside the kinds Snail.Sdk's session roots refuse a path with
/// (<see cref="Snail.Sdk.Mcp.Hosting.Sessions.Reached.Outside"/>, <see cref="Snail.Sdk.Mcp.Hosting.Sessions.Reached.NoRoot"/>)
/// and its own <see cref="Snail.Sdk.Mcp.Hosting.ToolFailures.Arguments"/> and
/// <see cref="Snail.Sdk.Mcp.Hosting.Sessions.GrantOptions.NotGranted"/>. Anything not named here that a tool throws is the
/// server's own failure, <see cref="Snail.Sdk.Mcp.Hosting.ToolFailures.Internal"/>, logged with its trace.
/// </remarks>
internal static class RefusalTypes
{
    /// <summary>A solution id this process holds no solution for: never loaded, unloaded, or dropped to stay within the ceiling.</summary>
    public const string NotLoaded = "notLoaded";

    /// <summary>A path, a file or a symbol that is not there.</summary>
    public const string NotFound = "notFound";

    /// <summary>A solution or project MSBuild could not open.</summary>
    public const string Unopened = "unopened";

    /// <summary>A file that is not markup of the loaded solution, given where only markup will do.</summary>
    public const string NotMarkup = "notMarkup";

    /// <summary>Lines that do not fit the file they name.</summary>
    public const string OutOfRange = "outOfRange";

    /// <summary>An answer past the response budget, refused whole rather than returned cut.</summary>
    public const string TooLarge = "tooLarge";
}
