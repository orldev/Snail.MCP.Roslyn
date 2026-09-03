using System.ComponentModel;
using ModelContextProtocol.Server;
using Snail.MCP.Roslyn.Tools.Prompts;

namespace Snail.MCP.Roslyn.Tools;

/// <summary>The escape hatch: one script against the warm solution, for questions no fixed tool anticipates.</summary>
[McpServerToolType]
public sealed class ScriptTools(IScriptRunner scripts)
{
    [McpServerTool(Name = "run_script")]
    [Description(ToolDescriptions.RunScript)]
    public Task<ScriptResult> RunScript(
        [Description("Solution id returned by load_solution.")]
        string solutionId,
        [Description("C# script body; its last expression is the returned value.")]
        string code,
        CancellationToken cancellationToken) =>
        scripts.RunAsync(solutionId, code, cancellationToken);
}
