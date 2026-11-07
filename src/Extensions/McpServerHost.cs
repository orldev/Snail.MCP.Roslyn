using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Snail.MCP.Roslyn.Configuration;
using Snail.Sdk.Mcp.Hosting;
using Snail.Sdk.Mcp.Http;

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

    /// <summary>The server answering sessions at <paramref name="port"/>, on every interface.</summary>
    /// <remarks>
    /// Bound on every interface because the callers are the workers of the machine, each in its own
    /// container: a server on loopback would be reachable from nothing but itself. The host is Snail.Sdk.Mcp.Http's
    /// container host, so log lines go to stderr and the heap is trimmed when idle as in every server of the fleet, and the
    /// endpoint is mapped behind its refusal of a request carrying <c>Origin</c>: a page in a browser always sends one, an
    /// agent's client never does. The host this server built of its own before answered such a request, so any page open
    /// on the machine could reach a server that runs scripts with no key. The browser is turned away before the session's
    /// root is looked at, so a page does not even learn which working copies the machine has.
    /// </remarks>
    internal static WebApplication Listening(string[] args, int port, IConfiguration settings)
    {
        var builder = McpHttpHost.CreateContainerBuilder(args, port);

        builder.Configuration.AddConfiguration(settings);
        builder.ConfigureMcpServerAtAnAddress(settings);

        var app = builder.Build();

        app.MapLocalMcp(Path);
        app.RefuseASessionItCannotServe();

        return app;
    }

    private static IHost Spoken(string[] args, IConfiguration settings)
    {
        var builder = McpHost.CreateBuilder(args);

        builder.ConfigureMcpServerAsAProgram(settings);

        return builder.Build();
    }
}
