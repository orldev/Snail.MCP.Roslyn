namespace Snail.MCP.Roslyn.Application.Ports;

/// <summary>Audits which declarations the solution actually uses and which its tests actually reach.</summary>
internal interface ICodeUsageAuditor
{
    Task<AuditPage> UnusedAsync(string solutionId, string? scope, int limit, CancellationToken cancellationToken);

    Task<AuditPage> UncoveredAsync(string solutionId, string? scope, int limit, CancellationToken cancellationToken);

    Task<IReadOnlyList<SymbolMatch>> TestsForAsync(
        string solutionId,
        SymbolQuery query,
        int limit,
        CancellationToken cancellationToken);
}
