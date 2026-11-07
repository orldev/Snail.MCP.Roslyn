using System.Collections;
using System.Net;
using System.Net.Sockets;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Snail.MCP.Roslyn.Adapters.Paths;
using Snail.MCP.Roslyn.Application;
using Snail.MCP.Roslyn.Configuration;
using Snail.MCP.Roslyn.Extensions;

namespace Snail.MCP.Roslyn.Tests;

/// <summary>The server at an address: where it learns the session's root from, and whom it turns away at the door.</summary>
public sealed class AddressTests
{
    /// <summary>The server as a deployment that says nothing starts it, whatever settings file this machine keeps.</summary>
    private static readonly Microsoft.Extensions.Configuration.IConfiguration NothingConfigured = WorkspaceSettings.Read(null, new Hashtable());

    [Fact]
    public void The_address_hands_over_every_root_it_names()
    {
        var context = new DefaultHttpContext();
        context.Request.QueryString = new QueryString("?root=/workspaces/one&root=/workspaces/two");

        Assert.Equal(["/workspaces/one", "/workspaces/two"], new HttpAddress(Accessor(context)).Values(SessionReach.Parameter));
    }

    /// <summary>Outside a request there is no address at all, and nothing to make a root up from.</summary>
    [Fact]
    public void Outside_a_request_the_address_says_nothing() =>
        Assert.Empty(new HttpAddress(Accessor(null)).Values(SessionReach.Parameter));

    [Fact]
    public void A_root_that_is_on_this_machine_is_not_reported_absent() =>
        Assert.Empty(SessionReach.Absent([Path.GetTempPath()]));

    [Fact]
    public void A_root_that_is_not_on_this_machine_is_named()
    {
        var elsewhere = Path.Combine(Path.GetTempPath(), $"snail-mcp-roslyn-{Guid.NewGuid():n}");

        Assert.Equal([elsewhere], SessionReach.Absent([elsewhere]));
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

        await using var server = McpServerHost.Listening([], FreePort(), NothingConfigured);
        await server.StartAsync();

        using var client = new HttpClient();
        var answer = await client.GetAsync(new Uri($"{Address(server)}{McpServerHost.Path}?root={Uri.EscapeDataString(elsewhere)}"));

        Assert.Equal(HttpStatusCode.BadRequest, answer.StatusCode);
        Assert.Contains(elsewhere, await answer.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    /// <summary>What it is all for: a session works in its own copy and does not reach the one beside it.</summary>
    [Fact]
    public async Task A_session_reaches_the_solution_of_its_own_root_and_not_the_one_beside_it()
    {
        var mine = Directory.CreateTempSubdirectory("snail-mcp-roslyn-mine").FullName;
        var theirs = Directory.CreateTempSubdirectory("snail-mcp-roslyn-theirs").FullName;

        try
        {
            await using var server = McpServerHost.Listening([], FreePort(), NothingConfigured);
            await server.StartAsync();

            var refusal = await Load(server, mine, Path.Combine(theirs, "Other.sln"));

            Assert.Contains("outside this session", refusal, StringComparison.Ordinal);
            Assert.Contains(mine, refusal, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(mine, recursive: true);
            Directory.Delete(theirs, recursive: true);
        }
    }

    private static async Task<string> Load(WebApplication server, string root, string solution)
    {
        await using var client = await McpClient.CreateAsync(new HttpClientTransport(new HttpClientTransportOptions
        {
            Endpoint = new Uri($"{Address(server)}{McpServerHost.Path}?root={Uri.EscapeDataString(root)}")
        }));

        var answer = await client.CallToolAsync("load_solution", new Dictionary<string, object?> { ["path"] = solution });

        return string.Join(string.Empty, answer.Content.OfType<TextContentBlock>().Select(block => block.Text));
    }


    /// <summary>
    /// A session without a root, at a server configured with none, would be served nothing anyway. Hearing
    /// that once at the door beats hearing it from every call for the whole step.
    /// </summary>
    [Fact]
    public async Task A_session_that_names_no_root_at_a_server_configured_with_none_is_turned_away()
    {
        await using var server = McpServerHost.Listening([], FreePort(), NothingConfigured);
        await server.StartAsync();

        using var client = new HttpClient();
        var answer = await client.GetAsync(new Uri($"{Address(server)}{McpServerHost.Path}"));

        Assert.Equal(HttpStatusCode.BadRequest, answer.StatusCode);
        Assert.Contains("named no root", await answer.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }


    /// <summary>
    /// A role's allow list held exactly until it was given a tool that calls other tools:
    /// run_tools reached everything the server has, including what the operator deliberately did not grant.
    /// Now the same address that names the root names what is allowed, and it is checked at the door of every call.
    /// </summary>
    [Fact]
    public async Task A_tool_the_session_was_not_granted_is_refused()
    {
        var copy = Directory.CreateTempSubdirectory("snail-grant").FullName;

        await using var server = McpServerHost.Listening([], FreePort(), NothingConfigured);
        await server.StartAsync();

        var address = $"{Address(server)}{McpServerHost.Path}?root={Uri.EscapeDataString(copy)}&tool=health_check";

        await using var client = await McpClient.CreateAsync(new HttpClientTransport(new HttpClientTransportOptions { Endpoint = new Uri(address) }));

        var refused = await client.CallToolAsync("run_script", new Dictionary<string, object?>());

        Assert.Equal(true, refused.IsError);
        Assert.Contains("was not granted",
            string.Join(string.Empty, refused.Content.OfType<TextContentBlock>().Select(block => block.Text)),
            StringComparison.Ordinal);
    }

    /// <summary>
    /// The grant is a call filter, and run_tools invokes each step's tool object itself, past it: a role given run_tools
    /// reached every tool of the server through it, the withheld ones included. A step is checked against the grant too.
    /// </summary>
    [Fact]
    public async Task RunTools_AStepTheSessionWasNotGranted_IsRefused()
    {
        var copy = Directory.CreateTempSubdirectory("snail-grant").FullName;

        await using var server = McpServerHost.Listening([], FreePort(), NothingConfigured);
        await server.StartAsync();

        var run = await RunAsync(server, copy, ["run_tools", "health_check"], "run_script", new JsonObject { ["code"] = "1 + 1" });

        Assert.False(run["ok"]!.GetValue<bool>());
        Assert.Equal(0, run["ran"]!.GetValue<int>());
        Assert.Contains("'run_script' was not granted", run["error"]!.GetValue<string>(), StringComparison.Ordinal);
    }

    /// <summary>Checking each step against the grant must not stop the steps the session was given.</summary>
    [Fact]
    public async Task RunTools_AStepTheSessionWasGranted_Runs()
    {
        var copy = Directory.CreateTempSubdirectory("snail-grant").FullName;

        await using var server = McpServerHost.Listening([], FreePort(), NothingConfigured);
        await server.StartAsync();

        var run = await RunAsync(server, copy, ["run_tools", "find_tool"], "find_tool", new JsonObject { ["task"] = "does it compile" });

        Assert.True(run["ok"]!.GetValue<bool>());
        Assert.Equal(1, run["ran"]!.GetValue<int>());
    }

    private static async Task<JsonNode> RunAsync(WebApplication server, string root, string[] granted, string tool, JsonObject parameters)
    {
        var grant = string.Concat(granted.Select(name => $"&tool={name}"));
        var address = $"{Address(server)}{McpServerHost.Path}?root={Uri.EscapeDataString(root)}{grant}";

        await using var client = await McpClient.CreateAsync(new HttpClientTransport(new HttpClientTransportOptions { Endpoint = new Uri(address) }));

        var answer = await client.CallToolAsync("run_tools", new Dictionary<string, object?>
        {
            ["steps"] = new JsonArray(new JsonObject { ["tool"] = tool, ["params"] = parameters })
        });

        return JsonNode.Parse(string.Concat(answer.Content.OfType<TextContentBlock>().Select(block => block.Text)))!;
    }

    /// <summary>
    /// A page in a browser always sends Origin and an agent's client never does. The server runs scripts and keys nothing,
    /// so a page open on the machine must not reach it — and is refused before the door looks at the root, which would
    /// otherwise tell it what this machine has.
    /// </summary>
    [Fact]
    public async Task Request_FromABrowser_IsRefused()
    {
        await using var server = McpServerHost.Listening([], FreePort(), NothingConfigured);
        await server.StartAsync();

        using var client = new HttpClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri($"{Address(server)}{McpServerHost.Path}"));
        request.Headers.Add("Origin", "https://example.com");

        using var answer = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, answer.StatusCode);
    }

    /// <summary>A port nothing listens on; the server's host takes a real port, since a port of zero is no published port.</summary>
    private static int FreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();

        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    /// <summary>The server listens on every interface; "::" cannot be called, so the port is taken with loopback.</summary>
    private static string Address(WebApplication server)
    {
        var bound = server.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();

        return $"http://127.0.0.1:{new Uri(bound.Replace("://*:", "://localhost:", StringComparison.Ordinal)).Port}";
    }

    private static IHttpContextAccessor Accessor(HttpContext? context) => new HttpContextAccessor { HttpContext = context };
}
