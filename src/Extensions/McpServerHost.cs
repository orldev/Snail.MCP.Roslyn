using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Snail.MCP.Roslyn.Configuration;

namespace Snail.MCP.Roslyn.Extensions;

/// <summary>The server as a running host: listening at an address when it was given a port, a child process otherwise.</summary>
/// <remarks>
/// The settings are the server's settings file and its <c>SNAIL_MCP_ROSLYN_</c> variables (<see cref="WorkspaceSettings"/>),
/// read once before there is a host, because the port among them decides which host is built at all.
/// </remarks>
public static class McpServerHost
{
    /// <summary>The path a session is opened at, and the one the platform writes into its registry.</summary>
    public const string Path = "/mcp";

    /// <summary>Builds whichever of the two shapes the configuration asks for.</summary>
    public static IHost Of(string[] args)
    {
        var settings = WorkspaceSettings.Read();
        var port = settings.GetValue<int>(nameof(WorkspaceOptions.Port));

        return port > 0 ? Listening(args, port, settings) : Spoken(args, settings);
    }

    /// <summary>The server answering sessions at <paramref name="port"/>; a port of zero takes any free one.</summary>
    /// <remarks>
    /// Bound on every interface because the callers are the workers of the machine, each in its own
    /// container: a server on loopback would be reachable from nothing but itself. That is why this host is not
    /// Snail.Sdk.Mcp.Http's, which listens on loopback only.
    /// </remarks>
    internal static WebApplication Listening(string[] args, int port, IConfiguration settings)
    {
        var builder = WebApplication.CreateSlimBuilder(args);

        builder.Configuration.AddConfiguration(settings);
        builder.ConfigureMcpServerAtAnAddress(settings);

        var app = builder.Build();

        app.Urls.Add($"http://*:{port}");
        app.RefuseASessionItCannotServe();
        app.MapMcp(Path);

        return app;
    }

    private static IHost Spoken(string[] args, IConfiguration settings)
    {
        var builder = Snail.Sdk.Mcp.Hosting.McpHost.CreateBuilder(args);

        builder.ConfigureMcpServerAsAProgram(settings);

        return builder.Build();
    }
}
