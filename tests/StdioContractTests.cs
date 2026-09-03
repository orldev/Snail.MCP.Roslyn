using System.Diagnostics;
using System.Text.Json;

namespace Snail.MCP.Roslyn.Tests;

/// <summary>Standard output belongs to the protocol; anything else there breaks every client.</summary>
public sealed class StdioContractTests
{
    [Fact]
    public async Task Standard_output_carries_nothing_but_protocol_messages()
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
    public async Task The_client_is_told_how_to_work_with_this_server_at_initialize()
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
    public async Task The_whole_catalogue_reaches_a_client_over_the_real_transport()
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
