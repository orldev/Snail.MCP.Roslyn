namespace Snail.MCP.Roslyn.Application.Ports;

/// <summary>Compiles a file's proposed contents in the context of the solution, before it reaches the disk.</summary>
internal interface ICodeValidator
{
    Task<ValidationResult> ValidateAsync(
        string solutionId,
        string file,
        string content,
        CancellationToken cancellationToken);
}
