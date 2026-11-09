using Snail.MCP.Roslyn.Extensions;
using Snail.Sdk.Mcp.Hosting.Sessions;

namespace Snail.MCP.Roslyn.Tests;

/// <summary>
/// How far the session in front of the server reaches. One process per caller never had this question:
/// whatever it was asked to load was its own. A container shared by a machine is another
/// animal: each worker has its own working copy, and the whole point is that nobody reaches another's.
/// The roots are Snail.Sdk's; these pin how this server configures them (<see cref="ServiceCollectionExtensions.Roots"/>).
/// </summary>
public sealed class SessionReachTests
{
    [Fact]
    public void A_session_that_named_a_root_reaches_inside_it_and_nowhere_else()
    {
        var roots = Opened(at: "/workspaces/task-1");

        Assert.True(roots.Reach("/workspaces/task-1/src/App.csproj").IsAllowed);
        Assert.False(roots.Reach("/workspaces/task-2/src/App.csproj").IsAllowed);
    }

    /// <summary>A sibling whose name merely begins the same way is the classic way out of the cage.</summary>
    [Fact]
    public void A_sibling_whose_name_merely_begins_the_same_way_is_not_inside() =>
        Assert.False(Opened(at: "/workspaces/task-1").Reach("/workspaces/task-1-secrets/App.csproj").IsAllowed);

    /// <summary>A server started for one caller names no root and is configured with none — and that is not a hole but how it is built.</summary>
    [Fact]
    public void A_program_configured_with_nothing_reaches_wherever_the_process_can() =>
        Assert.True(new SessionRoots(SessionAddress.None, [], Options).Reach("/anywhere/at/all.csproj").IsAllowed);

    /// <summary>At an address the same emptiness reaches nothing: the door turns such a session away, and a call that got past it would too.</summary>
    [Fact]
    public void A_session_at_an_address_that_named_nothing_at_a_server_configured_with_nothing_reaches_nothing() =>
        Assert.False(Opened(at: null).Reach("/anywhere/at/all.csproj").IsAllowed);

    /// <summary>A configured root stands where there is no address: that is how a server run as a program is confined.</summary>
    [Fact]
    public void A_configured_root_stands_when_the_address_names_none()
    {
        var roots = Opened(at: null, configured: "/workspaces");

        Assert.True(roots.Reach("/workspaces/task-1/App.csproj").IsAllowed);
        Assert.False(roots.Reach("/etc/passwd").IsAllowed);
    }

    /// <summary>A root named outside the configured ones is no working copy, whoever wrote the address: it reaches nothing.</summary>
    [Fact]
    public void A_root_named_outside_the_configured_ones_reaches_nothing() =>
        Assert.False(Opened(at: "/etc", configured: "/workspaces").Reach("/etc/passwd").IsAllowed);

    /// <summary>A symlink inside the root is the cheapest way out of it, so links are resolved.</summary>
    [Fact]
    public void A_symlink_that_leads_out_of_the_root_does_not_pass_as_a_path_inside_it()
    {
        var one = TestFolder.Create();
        var two = TestFolder.Create();

        try
        {
            var door = Path.Combine(one, "out");
            Directory.CreateSymbolicLink(door, two);

            Assert.False(Opened(at: one).Reach(Path.Combine(door, "App.csproj")).IsAllowed);
        }
        finally
        {
            Directory.Delete(one, recursive: true);
            Directory.Delete(two, recursive: true);
        }
    }

    /// <summary>
    /// A link in the MIDDLE of the path: ResolveLinkTarget answers only about the last segment, so
    /// /copy/out/solution.sln resolved to itself and escaped the root. Found on the filesystem server.
    /// </summary>
    [Fact]
    public void A_symlink_in_the_middle_of_the_path_does_not_pass_as_a_path_inside_the_root()
    {
        var one = TestFolder.Create();
        var two = TestFolder.Create();

        try
        {
            File.WriteAllText(Path.Combine(two, "Other.sln"), string.Empty);
            Directory.CreateSymbolicLink(Path.Combine(one, "out"), two);

            Assert.False(Opened(at: one).Reach(Path.Combine(one, "out", "Other.sln")).IsAllowed);
        }
        finally
        {
            Directory.Delete(one, recursive: true);
            Directory.Delete(two, recursive: true);
        }
    }

    /// <summary>The refusal names the root: otherwise a person reads "not allowed" and does not know where is.</summary>
    [Fact]
    public void The_refusal_names_where_the_session_does_work() =>
        Assert.Contains("/workspaces/task-1", Opened(at: "/workspaces/task-1").Reach("/elsewhere").Refusal, StringComparison.Ordinal);

    private static SessionRootsOptions Options
    {
        get
        {
            SessionRootsOptions options = new();
            ServiceCollectionExtensions.Roots(options);

            return options;
        }
    }

    private static SessionRoots Opened(string? at, string? configured = null) =>
        new(at is null ? new FixedAddress() : new FixedAddress((SessionRoots.Parameter, at)), configured is null ? [] : [configured], Options);
}
