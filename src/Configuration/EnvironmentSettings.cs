using System.Collections;
using Microsoft.Extensions.Configuration;

namespace Snail.MCP.Roslyn.Configuration;

/// <summary>Maps <c>SNAIL_MCP_ROSLYN_FIELD_NAME</c> variables onto the fields of <see cref="WorkspaceOptions"/>.</summary>
/// <remarks>
/// The reading is Snail.Sdk.Mcp's, the same in every Snail server; only the prefix is this server's. The settings are
/// built apart from the host's own configuration on purpose: an <c>appsettings.json</c> lying in the working directory
/// of whoever started the server, or a variable of theirs that happens to share a name, is not a setting of this server
/// and must not become one by accident.
/// </remarks>
public static class EnvironmentSettings
{
    /// <summary>What every variable this server reads begins with.</summary>
    public const string Prefix = "SNAIL_MCP_ROSLYN_";

    /// <summary>The prefixed variables as configuration keys, blank ones left out.</summary>
    public static IConfiguration Read(IDictionary? variables = null) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(Snail.Sdk.Mcp.Hosting.EnvironmentSettings.Read(Prefix, variables ?? Environment.GetEnvironmentVariables()))
            .Build();
}
