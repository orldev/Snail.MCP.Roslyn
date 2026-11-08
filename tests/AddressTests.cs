using System.Net;
using System.Text.Json.Nodes;
using Snail.MCP.Roslyn.Extensions;
using Snail.Sdk.Mcp.Testing;

namespace Snail.MCP.Roslyn.Tests;

/// <summary>
/// The server at an address, started as a program the way the platform starts it: whom it turns away at the door, how
/// far a session reaches, and what a session was granted.
/// </summary>
public sealed class AddressTests : IAsyncLifetime
{
    private const string Assembly = "Snail.MCP.Roslyn";

    private const string Variable = "SNAIL_ROSLYN_SERVER";

    private readonly string _copy = TestFolder.Create();

    private HttpServerProgram _server = null!;

    /// <summary>The server reads its address from SNAIL_MCP_ROSLYN_URL, is told HTTP by SNAIL_MCP_ROSLYN_TRANSPORT and is ready when /healthz answers.</summary>
    public async Task InitializeAsync() =>
        _server = await HttpServerProgram.StartAsync(Assembly, Variable);

    public async Task DisposeAsync()
    {
        await _server.DisposeAsync();

        Directory.Delete(_copy, recursive: true);
    }

    /// <summary>
    /// The worst failure of the address scheme: discovery hands the worker of machine B the server of machine A. A call
    /// does not show it — the server answers, only about a solution that is not the task's. So the session is not opened at all,
    /// and the refusal names the root.
    /// </summary>
    [Fact]
    public async Task A_session_opened_at_a_root_this_machine_does_not_have_is_refused_and_told_which()
    {
        var elsewhere = Path.Combine(Path.GetTempPath(), $"snail-mcp-roslyn-{Guid.NewGuid():n}");

        var (status, said) = await OpenAsync(_server, $"root={Uri.EscapeDataString(elsewhere)}");

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Contains(elsewhere, said, StringComparison.Ordinal);
    }

    /// <summary>What it is all for: a session works in its own copy and does not reach the one beside it.</summary>
    [Fact]
    public async Task A_session_reaches_the_solution_of_its_own_root_and_not_the_one_beside_it()
    {
        var theirs = TestFolder.Create();

        try
        {
            await using var client = await _server.SessionAsync($"root={Uri.EscapeDataString(_copy)}");

            var solution = Path.Combine(theirs, "Other.sln");

            var answer = await client.CallToolAsync("load_solution", new Dictionary<string, object?> { ["path"] = solution });

            Assert.True(answer.IsError, answer.Text());
            Assert.Contains($"'{solution}' is outside this request's root {_copy}, which its address names", answer.Error(), StringComparison.Ordinal);
            Assert.Equal("outside", answer.Type());
        }
        finally
        {
            Directory.Delete(theirs, recursive: true);
        }
    }

    /// <summary>
    /// A session without a root, at a server configured with none, would be served nothing anyway. Hearing
    /// that once at the door beats hearing it from every call for the whole step.
    /// </summary>
    [Fact]
    public async Task A_session_that_names_no_root_at_a_server_configured_with_none_is_turned_away()
    {
        var (status, said) = await OpenAsync(_server, null);

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Contains("named no root", said, StringComparison.Ordinal);
        Assert.Contains(ServiceCollectionExtensions.RootsSetting, said, StringComparison.Ordinal);
    }

    /// <summary>A server configured with the volume of working copies serves those alone, whatever root the address names.</summary>
    [Fact]
    public async Task A_session_opened_at_a_root_outside_the_configured_ones_is_turned_away()
    {
        var volume = TestFolder.Create();

        try
        {
            await using var confined = await HttpServerProgram.StartAsync(Assembly, Variable, new Dictionary<string, string?> { [ServiceCollectionExtensions.RootsSetting] = volume });

            var (status, said) = await OpenAsync(confined, $"root={Uri.EscapeDataString(_copy)}");

            Assert.Equal(HttpStatusCode.BadRequest, status);
            Assert.Contains("is not one of them", said, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(volume, recursive: true);
        }
    }

    /// <summary>
    /// A role's allow list held exactly until it was given a tool that calls other tools:
    /// run_tools reached everything the server has, including what the operator deliberately did not grant.
    /// Now the same address that names the root names what is allowed: a tool left out is not listed, and a call to it is refused.
    /// </summary>
    [Fact]
    public async Task A_tool_the_session_was_not_granted_is_hidden_and_refused()
    {
        await using var client = await _server.SessionAsync($"root={Uri.EscapeDataString(_copy)}&tool=health_check");

        var listed = await client.ListToolsAsync();
        var refused = await client.CallToolAsync("run_script", new Dictionary<string, object?>());

        Assert.Equal(["health_check"], listed.Select(tool => tool.Name));
        Assert.True(refused.IsError);
        Assert.Contains("'run_script' was not granted", refused.Error(), StringComparison.Ordinal);
    }

    /// <summary>
    /// run_tools invokes each step's tool itself, past the door: a role given run_tools reached every tool of the server
    /// through it, the withheld ones included. A step is held to the grant too.
    /// </summary>
    [Fact]
    public async Task RunTools_AStepTheSessionWasNotGranted_IsRefused()
    {
        await using var client = await _server.SessionAsync($"root={Uri.EscapeDataString(_copy)}&tool=run_tools&tool=health_check");

        var run = await client.CallToolAsync("run_tools", Steps(("run_script", new JsonObject { ["code"] = "1 + 1" })));

        Assert.False(run.IsOk());
        Assert.Contains("'run_script' was not granted", run.Error(), StringComparison.Ordinal);
        Assert.False(run.Data()!["steps"]![0]!["ok"]!.GetValue<bool>());
    }

    /// <summary>Holding each step to the grant must not stop the steps the session was given.</summary>
    [Fact]
    public async Task RunTools_AStepTheSessionWasGranted_Runs()
    {
        await using var client = await _server.SessionAsync($"root={Uri.EscapeDataString(_copy)}&tool=run_tools&tool=find_tool");

        var run = await client.CallToolAsync("run_tools", Steps(("find_tool", new JsonObject { ["task"] = "does it compile" })));

        Assert.True(run.IsOk(), run.Text());
        Assert.Equal(1, run.Data()!["ran"]!.GetValue<int>());
    }

    /// <summary>
    /// A page in a browser always sends Origin and an agent's client never does. The server runs scripts and keys nothing,
    /// so a page open on the machine must not reach it — and is refused before the door looks at the root, which would
    /// otherwise tell it what this machine has.
    /// </summary>
    [Fact]
    public async Task Endpoint_AsServed_RefusesBrowsersOnly() =>
        Assert.Empty(await HttpGuards.ProblemsAsync(_server.Endpoint));

    /// <summary>Every interface outside a container stops the start in one line that names the variable, with exit code 1 and no trace.</summary>
    [Fact]
    public async Task Start_OnEveryInterfaceOutsideAContainer_IsRefusedInOneLine()
    {
        var environment = new Dictionary<string, string?>
        {
            ["SNAIL_MCP_ROSLYN_LISTENS_ON_EVERY_INTERFACE"] = "true",
            ["DOTNET_RUNNING_IN_CONTAINER"] = null,
        };

        var refused = await Assert.ThrowsAsync<ServerExitedException>(() => HttpServerProgram.StartAsync(Assembly, Variable, environment));

        Assert.Equal(1, refused.ExitCode);
        Assert.Single(refused.Stderr.Split('\n'));
        Assert.Contains("SNAIL_MCP_ROSLYN_LISTENS_ON_EVERY_INTERFACE", refused.Stderr, StringComparison.Ordinal);
        Assert.DoesNotContain("   at ", refused.Stderr, StringComparison.Ordinal);
    }

    private static Dictionary<string, object?> Steps(params (string Tool, JsonObject Arguments)[] steps) => new()
    {
        ["steps"] = new JsonArray([.. steps.Select(step => (JsonNode)new JsonObject { ["tool"] = step.Tool, ["arguments"] = step.Arguments })])
    };

    /// <summary>A plain request at the endpoint with this query: what the door answers before any session exists.</summary>
    private static async Task<(HttpStatusCode Status, string Said)> OpenAsync(HttpServerProgram server, string? query)
    {
        using var client = new HttpClient();
        using var answer = await client.GetAsync(new UriBuilder(server.Endpoint) { Query = query ?? string.Empty }.Uri);

        return (answer.StatusCode, await answer.Content.ReadAsStringAsync());
    }
}
