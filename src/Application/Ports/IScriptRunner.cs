namespace Snail.MCP.Roslyn.Application.Ports;

/// <summary>Evaluates a C# script against the loaded solution, for the questions no fixed tool anticipates.</summary>
public interface IScriptRunner
{
    Task<ScriptResult> RunAsync(string solutionId, string code, CancellationToken cancellationToken);
}
