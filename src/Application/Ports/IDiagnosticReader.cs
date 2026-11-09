namespace Snail.MCP.Roslyn.Application.Ports;

/// <summary>Reports what the compiler says about the code as it stands on disk.</summary>
public interface IDiagnosticReader
{
    Task<IssuePage> ReadAsync(
        string solutionId,
        string? scope,
        IssueSeverity minimum,
        int limit,
        bool includeGenerated,
        bool analyzers,
        CancellationToken cancellationToken);
}
