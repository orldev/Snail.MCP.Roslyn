namespace Snail.MCP.Roslyn.Application.Ports;

/// <summary>Shows what source generators contributed, so a symbol with no declaration can be traced.</summary>
internal interface IGeneratedCodeReader
{
    Task<GeneratedCodePage> ReadAsync(
        string solutionId,
        string? project,
        string? hintName,
        bool includeText,
        int limit,
        CancellationToken cancellationToken);
}
