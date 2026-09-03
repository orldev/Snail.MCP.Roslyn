namespace Snail.MCP.Roslyn.Application.Ports;

/// <summary>Answers questions whose truth lives at the level of the whole solution.</summary>
public interface ISolutionSurveyor
{
    Task<SolutionOverview> OverviewAsync(string solutionId, int limit, CancellationToken cancellationToken);

    Task<ArchitectureReport> CheckAsync(string solutionId, int limit, CancellationToken cancellationToken);

    Task<HealthReport> HealthAsync(string solutionId, CancellationToken cancellationToken);
}
