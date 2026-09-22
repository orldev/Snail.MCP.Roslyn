using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Snail.MCP.Roslyn.Adapters.Paths;
using Snail.MCP.Roslyn.Application;
using Snail.MCP.Roslyn.Extensions;

namespace Snail.MCP.Roslyn.Tests;

/// <summary>Сервер по адресу: откуда он узнаёт корень сессии и кого не пускает на порог.</summary>
public sealed class AddressTests
{
    [Fact]
    public void The_address_hands_over_every_root_it_names()
    {
        var context = new DefaultHttpContext();
        context.Request.QueryString = new QueryString("?root=/workspaces/one&root=/workspaces/two");

        Assert.Equal(["/workspaces/one", "/workspaces/two"], new HttpAddress(Accessor(context)).Values(SessionReach.Parameter));
    }

    /// <summary>Вне запроса адреса нет вовсе, и выдумывать корень не из чего.</summary>
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
    /// Худшая ошибка схемы с адресами: обнаружение служб отдаёт воркеру машины B сервер машины A. В вызове
    /// это не видно — сервер отвечает, только про чужое решение. Поэтому сессия не открывается вовсе,
    /// и отказ называет корень.
    /// </summary>
    [Fact]
    public async Task A_session_opened_at_a_root_this_machine_does_not_have_is_refused_and_told_which()
    {
        var elsewhere = Path.Combine(Path.GetTempPath(), $"snail-mcp-roslyn-{Guid.NewGuid():n}");

        await using var server = McpServerHost.Listening([], port: 0);
        await server.StartAsync();

        using var client = new HttpClient();
        var answer = await client.GetAsync(new Uri($"{Address(server)}{McpServerHost.Path}?root={Uri.EscapeDataString(elsewhere)}"));

        Assert.Equal(HttpStatusCode.BadRequest, answer.StatusCode);
        Assert.Contains(elsewhere, await answer.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    /// <summary>То, ради чего всё: сессия работает в своей копии и не дотягивается до соседней.</summary>
    [Fact]
    public async Task A_session_reaches_the_solution_of_its_own_root_and_not_the_one_beside_it()
    {
        var mine = Directory.CreateTempSubdirectory("snail-mcp-roslyn-mine").FullName;
        var theirs = Directory.CreateTempSubdirectory("snail-mcp-roslyn-theirs").FullName;

        try
        {
            await using var server = McpServerHost.Listening([], port: 0);
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
    /// Сессия без корня на сервере, которому корней не настроили, всё равно не получит ничего. Услышать
    /// это один раз на пороге лучше, чем слышать от каждого вызова весь шаг.
    /// </summary>
    [Fact]
    public async Task A_session_that_names_no_root_at_a_server_configured_with_none_is_turned_away()
    {
        await using var server = McpServerHost.Listening([], port: 0);
        await server.StartAsync();

        using var client = new HttpClient();
        var answer = await client.GetAsync(new Uri($"{Address(server)}{McpServerHost.Path}"));

        Assert.Equal(HttpStatusCode.BadRequest, answer.StatusCode);
        Assert.Contains("named no root", await answer.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }


    /// <summary>
    /// Белый список роли держался ровно до того, как ей выдали инструмент, зовущий другие инструменты:
    /// run_tools дотягивался до всего, что есть у сервера, включая то, что оператор нарочно не выдал.
    /// Теперь разрешённое называет тот же адрес, что и корень, и проверяется оно на пороге вызова.
    /// </summary>
    [Fact]
    public async Task A_tool_the_session_was_not_granted_is_refused()
    {
        var copy = Directory.CreateTempSubdirectory("snail-grant").FullName;

        await using var server = McpServerHost.Listening([], port: 0);
        await server.StartAsync();

        var address = $"{Address(server)}{McpServerHost.Path}?root={Uri.EscapeDataString(copy)}&tool=health_check";

        await using var client = await McpClient.CreateAsync(new HttpClientTransport(new HttpClientTransportOptions { Endpoint = new Uri(address) }));

        var refused = await client.CallToolAsync("run_script", new Dictionary<string, object?>());

        Assert.Equal(true, refused.IsError);
        Assert.Contains("was not granted",
            string.Join(string.Empty, refused.Content.OfType<TextContentBlock>().Select(block => block.Text)),
            StringComparison.Ordinal);
    }

    /// <summary>Сервер слушает на всех интерфейсах; обращаться к «::» нельзя, поэтому берётся порт и петля.</summary>
    private static string Address(WebApplication server)
    {
        var bound = server.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();

        return $"http://127.0.0.1:{new Uri(bound.Replace("://*:", "://localhost:", StringComparison.Ordinal)).Port}";
    }

    private static IHttpContextAccessor Accessor(HttpContext? context) => new HttpContextAccessor { HttpContext = context };
}
