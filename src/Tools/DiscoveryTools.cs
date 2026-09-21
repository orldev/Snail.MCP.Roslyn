using System.ComponentModel;
using System.Text.Json.Nodes;
using ModelContextProtocol.Server;
using Snail.MCP.Roslyn.Tools.Prompts;

namespace Snail.MCP.Roslyn.Tools;

/// <summary>Finding the way in: which of thirty tools answers the question at hand.</summary>
[McpServerToolType]
public sealed class DiscoveryTools(ToolIndex index)
{
    [McpServerTool(Name = "find_tool")]
    [Description(ToolDescriptions.FindTool)]
    public JsonObject FindTool(
        [Description(ToolDescriptions.FindToolTask)] string task,
        [Description(ToolDescriptions.FindToolLimit)] int limit = 5) =>
        index.Answer(task, limit);
}
