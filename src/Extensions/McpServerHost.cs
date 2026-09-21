using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Snail.MCP.Roslyn.Configuration;

namespace Snail.MCP.Roslyn.Extensions;

/// <summary>The server as a running host: listening at an address when it was given a port, a child process otherwise.</summary>
public static class McpServerHost
{
    /// <summary>The path a session is opened at, and the one the platform writes into its registry.</summary>
    public const string Path = "/mcp";

    /// <summary>Builds whichever of the two shapes the configuration asks for.</summary>
    public static IHost Of(string[] args) => Port(args) is var port and > 0 ? Listening(args, port) : Spoken(args);

    /// <summary>The server answering sessions at <paramref name="port"/>; a port of zero takes any free one.</summary>
    /// <remarks>
    /// Bound on every interface because the callers are the workers of the machine, each in its own
    /// container: a server on loopback would be reachable from nothing but itself.
    /// </remarks>
    internal static WebApplication Listening(string[] args, int port)
    {
        var builder = WebApplication.CreateSlimBuilder(args);

        builder.ConfigureMcpServerAtAnAddress();

        var app = builder.Build();

        app.Urls.Add($"http://*:{port}");
        app.RefuseASessionItCannotServe();
        app.MapMcp(Path);

        return app;
    }

    private static IHost Spoken(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        builder.ConfigureMcpServerAsAProgram();

        return builder.Build();
    }

    /// <summary>The port the configuration names, read before there is a host to read it into.</summary>
    /// <remarks>
    /// Read first and separately because the answer decides which host is built at all, and the sources
    /// mirror the ones the host itself would use so that a port set in either place is seen.
    /// </remarks>
    private static int Port(string[] args) =>
        new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .AddCommandLine(args)
            .Build()
            .GetSection(WorkspaceOptions.Section)
            .GetValue<int>(nameof(WorkspaceOptions.Port));
}
