namespace Snail.MCP.Roslyn.Application.Ports;

/// <summary>Plans the extraction of a markup block into a new component; returns edits and never writes.</summary>
public interface IComponentExtractor
{
    Task<ExtractionPlan> ExtractAsync(string solutionId, ExtractionRequest request, CancellationToken cancellationToken);
}
