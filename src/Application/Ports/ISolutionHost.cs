namespace Snail.MCP.Roslyn.Application.Ports;

/// <summary>Opens solutions, keeps their compilations warm, and releases them on request.</summary>
public interface ISolutionHost
{
    Task<WorkspaceReport> LoadAsync(string path, CancellationToken cancellationToken, bool countDiagnostics = false);

    UnloadReport Unload(string solutionId);
}
