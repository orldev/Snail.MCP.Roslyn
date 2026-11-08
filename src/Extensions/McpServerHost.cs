using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Snail.MCP.Roslyn.Configuration;
using Snail.Sdk.Mcp.Hosting;
using Snail.Sdk.Mcp.Http;

namespace Snail.MCP.Roslyn.Extensions;

/// <summary>The server as a running host: listening at an address when its settings ask for HTTP, a child process otherwise.</summary>
/// <remarks>
/// The settings are the server's settings file and its <c>SNAIL_MCP_ROSLYN_</c> variables (<see cref="WorkspaceSettings"/>),
/// read once before there is a host, because the endpoint among them decides which host is built at all: a port, or
/// <c>Transport=Http</c> with a <c>Url</c>, means HTTP, and neither means stdio.
/// </remarks>
public static class McpServerHost
{
    /// <summary>The path a session is opened at, and the one the platform writes into its registry.</summary>
    public const string Path = McpHttpHost.DefaultPath;

    /// <summary>Where the server listens when its settings ask for HTTP and name no address: the port the platform registers.</summary>
    public static readonly Uri DefaultUrl = new("http://127.0.0.1:8817/");

    /// <summary>Builds whichever of the two shapes the configuration asks for.</summary>
    public static IHost Of(string[] args)
    {
        var settings = WorkspaceSettings.Read();
        var endpoint = McpEndpoint.Read(settings, DefaultUrl);

        return endpoint.Transport is McpTransport.Http ? Listening(args, endpoint, settings) : Spoken(args, settings);
    }

    /// <summary>The server answering sessions at <paramref name="endpoint"/>.</summary>
    /// <remarks>
    /// Inside a container on every interface, because the callers are the workers of the machine, each in its own
    /// container, and the published port does not reach a container's loopback; anywhere else on loopback, unless the
    /// settings ask for every interface. The host is Snail.Sdk.Mcp.Http's: log lines go to stderr, the heap is trimmed
    /// when idle, and the endpoint is mapped behind its refusal of a request carrying <c>Origin</c> — a page in a browser
    /// always sends one, an agent's client never does, and this server runs scripts with no key. The browser is turned
    /// away before the session's root is looked at, so a page does not even learn which working copies the machine has;
    /// then a session naming a root this machine does not have, a root outside the configured roots, or no root where
    /// none is configured.
    /// </remarks>
    private static WebApplication Listening(string[] args, McpEndpoint endpoint, IConfiguration settings)
    {
        var builder = McpHttpHost.CreateBuilder(args, endpoint);

        builder.Configuration.AddConfiguration(settings);
        builder.ConfigureMcpServerAtAnAddress(settings);

        var app = builder.Build();

        app.MapLocalMcp(Path);
        app.MapHealth();
        app.RefuseSessionsItCannotServe(ServiceCollectionExtensions.Volumes, Path);

        return app;
    }

    private static IHost Spoken(string[] args, IConfiguration settings)
    {
        var builder = McpHost.CreateBuilder(args);

        builder.ConfigureMcpServerAsAProgram(settings);

        return builder.Build();
    }
}
