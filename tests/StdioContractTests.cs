using System.Diagnostics;
using System.Text.Json;

namespace Snail.MCP.Roslyn.Tests;

public sealed class StdioContractTests
{
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

        Assert.Equal(30, tools.GetArrayLength());
        Assert.All(
            tools.EnumerateArray(),
            tool => Assert.False(string.IsNullOrWhiteSpace(tool.GetProperty("description").GetString())));
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

    private static Process Start()
    {
        var server = Path.Combine(
            RepositoryPaths.Root, "src", "bin", Configuration(), "net10.0", "Snail.MCP.Roslyn.dll");

        Assert.True(File.Exists(server), $"Build the server before running this test: {server}");

        return Process.Start(new ProcessStartInfo("dotnet", server)
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        })!;
    }

    private static string Configuration() =>
#if DEBUG
        "Debug";
#else
        "Release";
#endif
}
