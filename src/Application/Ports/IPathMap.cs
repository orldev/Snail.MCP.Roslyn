namespace Snail.MCP.Roslyn.Application.Ports;

/// <summary>Translates between the caller's filesystem roots and this process's roots.</summary>
public interface IPathMap
{
    string ToProcess(string callerPath);

    string ToCaller(string processPath);
}
