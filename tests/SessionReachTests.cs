using Microsoft.Extensions.Options;
using Snail.MCP.Roslyn.Application;
using Snail.MCP.Roslyn.Configuration;

namespace Snail.MCP.Roslyn.Tests;

/// <summary>
/// How far the session in front of the server reaches. One process per caller never had this question:
/// whatever it was asked to load was its own. A container shared by a machine is another
/// animal: each worker has its own working copy, and the whole point is that nobody reaches another's.
/// </summary>
public sealed class SessionReachTests
{
    [Fact]
    public void A_session_that_named_a_root_reaches_inside_it_and_nowhere_else()
    {
        var reach = Opened(at: "/workspaces/task-1");

        Assert.True(reach.Allows("/workspaces/task-1/src/App.csproj"));
        Assert.False(reach.Allows("/workspaces/task-2/src/App.csproj"));
    }

    /// <summary>A sibling whose name merely begins the same way is the classic way out of the cage.</summary>
    [Fact]
    public void A_sibling_whose_name_merely_begins_the_same_way_is_not_inside() =>
        Assert.False(Opened(at: "/workspaces/task-1").Allows("/workspaces/task-1-secrets/App.csproj"));

    /// <summary>A server started for one caller names no root — and that is not a hole but how it is built.</summary>
    [Fact]
    public void A_session_that_named_nothing_reaches_wherever_the_process_can() =>
        Assert.True(Opened(at: null).Allows("/anywhere/at/all.csproj"));

    /// <summary>A configured root stands where there is no address: that is how a server run as a program is confined.</summary>
    [Fact]
    public void A_configured_root_stands_when_the_address_names_none()
    {
        var reach = Opened(at: null, configured: "/workspaces");

        Assert.True(reach.Allows("/workspaces/task-1/App.csproj"));
        Assert.False(reach.Allows("/etc/passwd"));
    }

    /// <summary>A symlink inside the root is the cheapest way out of it, so links are resolved.</summary>
    [Fact]
    public void A_symlink_that_leads_out_of_the_root_does_not_pass_as_a_path_inside_it()
    {
        var one = Directory.CreateTempSubdirectory("roslyn-reach-one").FullName;
        var two = Directory.CreateTempSubdirectory("roslyn-reach-two").FullName;

        try
        {
            var door = Path.Combine(one, "out");
            Directory.CreateSymbolicLink(door, two);

            Assert.False(Opened(at: one).Allows(Path.Combine(door, "App.csproj")));
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
        var one = Directory.CreateTempSubdirectory("roslyn-reach-mid-one").FullName;
        var two = Directory.CreateTempSubdirectory("roslyn-reach-mid-two").FullName;

        try
        {
            File.WriteAllText(Path.Combine(two, "Other.sln"), string.Empty);
            Directory.CreateSymbolicLink(Path.Combine(one, "out"), two);

            Assert.False(Opened(at: one).Allows(Path.Combine(one, "out", "Other.sln")));
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
        Assert.Contains("/workspaces/task-1", Opened(at: "/workspaces/task-1").Refusal("/elsewhere"), StringComparison.Ordinal);

    private static SessionReach Opened(string? at, string? configured = null) =>
        new(Options.Create(new WorkspaceOptions { SessionRoots = configured is null ? [] : [configured] }),
            new Address(at));

    private sealed class Address(string? root) : ISessionAddress
    {
        public IReadOnlyList<string> Values(string parameter) =>
            parameter == SessionReach.Parameter && root is not null ? [root] : [];
    }
}
