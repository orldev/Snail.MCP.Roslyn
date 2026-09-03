namespace Snail.MCP.Roslyn.Application.Ports;

/// <summary>Measures the blast radius of touching one symbol, grouped by project.</summary>
public interface IImpactAnalyzer
{
    Task<ImpactReport> AnalyzeAsync(string solutionId, SymbolQuery query, int limit, CancellationToken cancellationToken);
}
