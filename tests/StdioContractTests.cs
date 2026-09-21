using System.Diagnostics;
using System.Text.Json;

namespace Snail.MCP.Roslyn.Tests;

public sealed class StdioContractTests
{
    /// <summary>Bytes the tool catalogue may occupy, a little above what it occupies now.</summary>
    /// <remarks>
    /// Raised once, from 28 500, and deliberately: find_tool and run_tools cost about a kilobyte of the
    /// charge every caller pays all day, and what they buy is the other way round — a role that carries a
    /// handful of tools and asks for the rest by name, and three questions answered in one round trip
    /// instead of three. The trade is only worth it while those two stay short, which is why their own
    /// words live in the index and not in their descriptions.
    /// </remarks>
    private const int Ceiling = 29_500;

    [Fact]
    public async Task StandardOutput_AfterInitialize_CarriesOnlyJsonRpcMessages()
    {
        using var server = Start();

        await server.StandardInput.WriteLineAsync(
            """
            {"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"contract","version":"0"}}}
            """);
        await server.StandardInput.FlushAsync(CancellationToken.None);

        var line = await server.StandardOutput.ReadLineAsync(CancellationToken.None);

        server.StandardInput.Close();
        await server.WaitForExitAsync(CancellationToken.None);

        Assert.False(string.IsNullOrWhiteSpace(line), "The server answered nothing on standard output.");

        var message = JsonDocument.Parse(line!);

        Assert.Equal("2.0", message.RootElement.GetProperty("jsonrpc").GetString());
        Assert.True(message.RootElement.TryGetProperty("result", out _));
    }

    [Fact]
    public async Task Initialize_AnyClient_ReturnsServerInstructions()
    {
        using var server = Start();

        await server.StandardInput.WriteLineAsync(
            """
            {"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"contract","version":"0"}}}
            """);
        await server.StandardInput.FlushAsync(CancellationToken.None);

        var handshake = await ReadUntilAsync(server, 1);

        server.StandardInput.Close();
        await server.WaitForExitAsync(CancellationToken.None);

        Assert.NotNull(handshake);

        var instructions = handshake!.RootElement.GetProperty("result").GetProperty("instructions").GetString();

        Assert.False(string.IsNullOrWhiteSpace(instructions));
        Assert.Contains("get_diagnostics, not `dotnet build`", instructions, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ToolsList_OverStdioTransport_ReturnsWholeDescribedCatalogue()
    {
        using var server = Start();

        await server.StandardInput.WriteLineAsync(
            """
            {"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"contract","version":"0"}}}
            """);
        await server.StandardInput.WriteLineAsync("""{"jsonrpc":"2.0","method":"notifications/initialized"}""");
        await server.StandardInput.WriteLineAsync("""{"jsonrpc":"2.0","id":2,"method":"tools/list"}""");
        await server.StandardInput.FlushAsync(CancellationToken.None);

        var listing = await ReadUntilAsync(server, 2);

        server.StandardInput.Close();
        await server.WaitForExitAsync(CancellationToken.None);

        Assert.NotNull(listing);

        var tools = listing!.RootElement.GetProperty("result").GetProperty("tools");

        Assert.Equal(32, tools.GetArrayLength());
        Assert.All(
            tools.EnumerateArray(),
            tool => Assert.False(string.IsNullOrWhiteSpace(tool.GetProperty("description").GetString())));

        // The progress sink is a parameter of the method, not of the tool: the caller asks for notifications
        // with a progressToken, and a schema property named after it would be a second, wrong way to ask.
        Assert.All(
            tools.EnumerateArray(),
            tool => Assert.DoesNotContain(
                "progress",
                tool.GetProperty("inputSchema").GetRawText(),
                StringComparison.OrdinalIgnoreCase));

        // The catalogue is not sent once: the client carries it in every request it makes, so its size is a
        // standing charge on the caller's context. A tool or a sentence added here is paid for all day.
        Assert.True(
            tools.GetRawText().Length < Ceiling,
            $"The tool catalogue is {tools.GetRawText().Length} bytes against a ceiling of {Ceiling}. Shorten what was "
            + "added, or raise the ceiling deliberately and say what the caller gets for the context it costs.");
    }

    [Fact]
    public async Task CallTool_FailingCall_AnswersTheCallerAndLeavesTheTraceOnStandardError()
    {
        using var server = Start();

        await server.StandardInput.WriteLineAsync(
            """
            {"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"contract","version":"0"}}}
            """);
        await server.StandardInput.WriteLineAsync("""{"jsonrpc":"2.0","method":"notifications/initialized"}""");
        await server.StandardInput.WriteLineAsync(
            """
            {"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"get_diagnostics","arguments":{"solutionId":"never-loaded"}}}
            """);
        await server.StandardInput.FlushAsync(CancellationToken.None);

        var answer = await ReadUntilAsync(server, 2);

        Assert.NotNull(answer);

        var content = answer!.RootElement.GetProperty("result").GetProperty("content")[0].GetProperty("text").GetString();

        Assert.Contains("not loaded", content ?? string.Empty, StringComparison.Ordinal);

        server.StandardInput.Close();

        var logged = await server.StandardError.ReadToEndAsync(CancellationToken.None);

        await server.WaitForExitAsync(CancellationToken.None);

        Assert.Contains("get_diagnostics", logged, StringComparison.Ordinal);
        Assert.Contains(nameof(InvalidOperationException), logged, StringComparison.Ordinal);
    }

    /// <summary>
    /// Три вопроса одним заходом вместо трёх: ровно то, ради чего run_tools и существует. Шаги здесь
    /// нарочно такие, которым не нужно загруженное решение, — проверяется оболочка, а не Roslyn.
    /// </summary>
    [Fact]
    public async Task RunTools_SeveralSteps_AnswersEachOfThemInOneCall()
    {
        using var server = Start();

        await HandshakeAsync(server);
        await server.StandardInput.WriteLineAsync(
            """
            {"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"run_tools","arguments":{"steps":[{"tool":"find_tool","params":{"task":"does it compile"}},{"tool":"find_tool","params":{"task":"rename safely"}}]}}}
            """);
        await server.StandardInput.FlushAsync(CancellationToken.None);

        var answer = await ReadUntilAsync(server, 2);

        server.StandardInput.Close();
        await server.WaitForExitAsync(CancellationToken.None);

        Assert.NotNull(answer);

        var said = answer!.RootElement.GetProperty("result").GetProperty("content")[0].GetProperty("text").GetString()!;

        Assert.Contains("\"ran\":2", said, StringComparison.Ordinal);
        Assert.Contains("get_diagnostics", said, StringComparison.Ordinal);
        Assert.Contains("rename_symbol", said, StringComparison.Ordinal);
    }

    /// <summary>Шаг, называющий несуществующий инструмент, останавливает заход и говорит, куда смотреть.</summary>
    [Fact]
    public async Task RunTools_AStepNamingNoToolOfThisServer_StopsAndSaysWhereToLook()
    {
        using var server = Start();

        await HandshakeAsync(server);
        await server.StandardInput.WriteLineAsync(
            """
            {"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"run_tools","arguments":{"steps":[{"tool":"make_coffee","params":{}}]}}}
            """);
        await server.StandardInput.FlushAsync(CancellationToken.None);

        var answer = await ReadUntilAsync(server, 2);

        server.StandardInput.Close();
        await server.WaitForExitAsync(CancellationToken.None);

        var said = answer!.RootElement.GetProperty("result").GetProperty("content")[0].GetProperty("text").GetString()!;

        Assert.Contains("does not name a tool", said, StringComparison.Ordinal);
        Assert.Contains("find_tool", said, StringComparison.Ordinal);
    }

    private static async Task HandshakeAsync(System.Diagnostics.Process server)
    {
        await server.StandardInput.WriteLineAsync(
            """
            {"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"contract","version":"0"}}}
            """);
        await server.StandardInput.WriteLineAsync("""{"jsonrpc":"2.0","method":"notifications/initialized"}""");
    }

    /// <summary>The budget is set below any real answer, so the smallest successful call is over it.</summary>
    [Fact]
    public async Task CallTool_AnswerOverTheResponseBudget_IsRefusedWithItsSize()
    {
        using var server = Start(("Workspace__MaxResponseChars", "200"));

        await server.StandardInput.WriteLineAsync(
            """
            {"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"contract","version":"0"}}}
            """);
        await server.StandardInput.WriteLineAsync("""{"jsonrpc":"2.0","method":"notifications/initialized"}""");
        await server.StandardInput.WriteLineAsync(
            """
            {"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"load_solution","arguments":{"path":"ROOT"}}}
            """.Replace("ROOT", RepositoryPaths.Root.Replace("\\", "\\\\", StringComparison.Ordinal), StringComparison.Ordinal));
        await server.StandardInput.FlushAsync(CancellationToken.None);

        var answer = await ReadUntilAsync(server, 2);

        server.StandardInput.Close();
        await server.WaitForExitAsync(CancellationToken.None);

        Assert.NotNull(answer);

        var result = answer!.RootElement.GetProperty("result");
        var text = result.GetProperty("content")[0].GetProperty("text").GetString() ?? string.Empty;

        Assert.True(result.GetProperty("isError").GetBoolean(), text);
        Assert.Contains("against a budget of 200", text, StringComparison.Ordinal);
        Assert.Contains("load_solution", text, StringComparison.Ordinal);
    }

    /// <summary>Loading a solution takes seconds, and a caller that hears nothing cannot tell it from a hang.</summary>
    [Fact]
    public async Task CallTool_LoadingASolutionWithAProgressToken_ReportsEachProjectAsItIsRead()
    {
        using var server = Start();

        await server.StandardInput.WriteLineAsync(
            """
            {"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"contract","version":"0"}}}
            """);
        await server.StandardInput.WriteLineAsync("""{"jsonrpc":"2.0","method":"notifications/initialized"}""");
        await server.StandardInput.WriteLineAsync(
            """
            {"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"_meta":{"progressToken":"load"},"name":"load_solution","arguments":{"path":"ROOT"}}}
            """.Replace("ROOT", RepositoryPaths.Root.Replace("\\", "\\\\", StringComparison.Ordinal), StringComparison.Ordinal));
        await server.StandardInput.FlushAsync(CancellationToken.None);

        List<string> reported = [];

        while (await server.StandardOutput.ReadLineAsync(CancellationToken.None) is { } line)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var message = JsonDocument.Parse(line).RootElement;

            if (message.TryGetProperty("method", out var method)
                && method.GetString() == "notifications/progress"
                && message.GetProperty("params").TryGetProperty("message", out var what))
                reported.Add(what.GetString() ?? string.Empty);

            if (message.TryGetProperty("id", out var id) && id.GetInt32() == 2)
                break;
        }

        server.StandardInput.Close();
        await server.WaitForExitAsync(CancellationToken.None);

        Assert.Contains(reported, step => step.Contains("Snail.MCP.Roslyn.csproj", StringComparison.Ordinal));
    }

    private static async Task<JsonDocument?> ReadUntilAsync(Process server, int id)
    {
        while (await server.StandardOutput.ReadLineAsync(CancellationToken.None) is { } line)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var message = JsonDocument.Parse(line);

            if (message.RootElement.TryGetProperty("id", out var found) && found.GetInt32() == id)
                return message;
        }

        return null;
    }

    private static Process Start(params (string Name, string Value)[] environment)
    {
        var server = Path.Combine(
            RepositoryPaths.Root, "src", "bin", Configuration(), "net10.0", "Snail.MCP.Roslyn.dll");

        Assert.True(File.Exists(server), $"Build the server before running this test: {server}");

        var start = new ProcessStartInfo("dotnet", server)
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        foreach (var (name, value) in environment)
            start.Environment[name] = value;

        return Process.Start(start)!;
    }

    private static string Configuration() =>
#if DEBUG
        "Debug";
#else
        "Release";
#endif
}
