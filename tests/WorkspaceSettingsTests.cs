using System.Collections;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Snail.MCP.Roslyn.Configuration;
using Snail.MCP.Roslyn.Extensions;

namespace Snail.MCP.Roslyn.Tests;

/// <summary>The settings file and the variables reach <see cref="WorkspaceOptions"/> the way the server binds it.</summary>
public sealed class WorkspaceSettingsTests
{
    [Fact]
    public void Read_ASettingsFile_ReachesTheOptions()
    {
        var folder = Directory.CreateTempSubdirectory("snail-mcp-roslyn-settings");

        try
        {
            var file = Written(folder, """{ "MaxSolutions": 2, "RestoreOnLoad": false, "SessionRoots": ["/workspaces"] }""");

            var options = Bound(WorkspaceSettings.Read(file, new Hashtable()));

            Assert.Equal(2, options.MaxSolutions);
            Assert.False(options.ShouldRestoreOnLoad);
            Assert.Equal(["/workspaces"], options.SessionRoots);
        }
        finally
        {
            folder.Delete(recursive: true);
        }
    }

    /// <summary>A variable is what a deployment says for this one run, so it overrules the file a person keeps.</summary>
    [Fact]
    public void Read_AVariableAndTheSettingsFile_TheVariableWins()
    {
        var folder = Directory.CreateTempSubdirectory("snail-mcp-roslyn-settings");

        try
        {
            var file = Written(folder, """{ "MaxSolutions": 2, "MaxRunSteps": 5 }""");
            var variables = new Hashtable { ["SNAIL_MCP_ROSLYN_MAX_SOLUTIONS"] = "7" };

            var options = Bound(WorkspaceSettings.Read(file, variables));

            Assert.Equal(7, options.MaxSolutions);
            Assert.Equal(5, options.MaxRunSteps);
        }
        finally
        {
            folder.Delete(recursive: true);
        }
    }

    [Fact]
    public void Read_AVariableOfAnotherServer_IsNotASetting()
    {
        var variables = new Hashtable { ["SNAIL_MCP_GIT_MAX_SOLUTIONS"] = "7", ["MAX_SOLUTIONS"] = "7" };

        Assert.Equal(new WorkspaceOptions().MaxSolutions, Bound(WorkspaceSettings.Read(null, variables)).MaxSolutions);
    }

    private static string Written(DirectoryInfo folder, string json)
    {
        var file = Path.Combine(folder.FullName, "config.json");
        File.WriteAllText(file, json);

        return file;
    }

    private static WorkspaceOptions Bound(Microsoft.Extensions.Configuration.IConfiguration settings)
    {
        using var provider = new ServiceCollection().AddCodeAnalysis(settings).BuildServiceProvider();

        return provider.GetRequiredService<IOptions<WorkspaceOptions>>().Value;
    }
}
