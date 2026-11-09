namespace Snail.MCP.Roslyn.Application.Ports;

/// <summary>Opens solutions, keeps their compilations warm, and releases them on request.</summary>
internal interface ISolutionHost
{
    Task<WorkspaceReport> LoadAsync(
        string path,
        CancellationToken cancellationToken,
        bool countDiagnostics = false,
        IProgress<WorkStep>? progress = null);

    Task<UnloadReport> UnloadAsync(string solutionId, CancellationToken cancellationToken);
}
