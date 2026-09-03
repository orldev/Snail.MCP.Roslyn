namespace Snail.MCP.Roslyn.Application.Ports;

/// <summary>Reads one file with everything the compiler knows about it, so plain text reads become unnecessary.</summary>
public interface IFileReader
{
    Task<FileView> ReadAsync(
        string solutionId,
        string file,
        int startLine,
        int maxLines,
        CancellationToken cancellationToken);
}
