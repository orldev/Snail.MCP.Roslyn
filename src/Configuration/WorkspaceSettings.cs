using System.Collections;
using Microsoft.Extensions.Configuration;
using Snail.Sdk.Mcp.Hosting;

namespace Snail.MCP.Roslyn.Configuration;

/// <summary>The settings <see cref="WorkspaceOptions"/> is bound from: the server's settings file, then its <c>SNAIL_MCP_ROSLYN_</c> variables, which win.</summary>
/// <remarks>
/// The file is the first of Snail.Sdk's chain, <c>~/.config/s3l-mcp-roslyn/config.json</c> among it, and the reading is
/// the same in every Snail server; only the name is this server's. The settings are built apart from the host's own
/// configuration on purpose: an <c>appsettings.json</c> lying in the working directory of whoever started the server,
/// or a variable of theirs that happens to share a name, is not a setting of this server and must not become one by
/// accident.
/// </remarks>
internal static class WorkspaceSettings
{
    /// <summary>The server's short name, which names its settings file and the prefix of its variables.</summary>
    public const string Name = "roslyn";

    /// <summary>The settings as they stand: the settings file found, then the process's variables.</summary>
    public static IConfiguration Read() => Read(ServerSettings.FileOf(Name), Environment.GetEnvironmentVariables());

    /// <summary>The settings of <paramref name="file"/>, when there is one, under the server's variables among <paramref name="variables"/>.</summary>
    public static IConfiguration Read(string? file, IDictionary variables) =>
        new ConfigurationBuilder()
            .AddServerSettings(Name, file, variables)
            .Build();
}
