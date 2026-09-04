namespace Snail.MCP.Roslyn.Application.Ports;

/// <summary>Runs analyzers over the loaded solution and offers the repairs they carry.</summary>
public interface IAnalyzerRunner
{
    Task<AnalyzerPage> RunAsync(
        string solutionId,
        string? scope,
        string? category,
        IssueSeverity minimum,
        int limit,
        CancellationToken cancellationToken,
        IProgress<WorkStep>? progress = null);

    Task<CodeFixPage> FixesAsync(
        string solutionId,
        string file,
        int line,
        string? diagnosticId,
        CancellationToken cancellationToken);
}
