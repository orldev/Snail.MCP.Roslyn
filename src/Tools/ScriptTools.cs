using System.ComponentModel;
using ModelContextProtocol.Server;
using Snail.MCP.Roslyn.Tools.Prompts;

namespace Snail.MCP.Roslyn.Tools;

[McpServerToolType]
public sealed class ScriptTools(IScriptRunner scripts)
{
    [McpServerTool(Name = "run_script", ReadOnly = false, Destructive = true, OpenWorld = true)]
    [Description(ToolDescriptions.RunScript)]
    public Task<ScriptResult> RunScript(
        [Description(ParameterDescriptions.Solution)]
        string solutionId,
        [Description("C# script body; its last expression is the returned value.")]
        string code,
        CancellationToken cancellationToken) =>
        scripts.RunAsync(solutionId, code, cancellationToken);
}
