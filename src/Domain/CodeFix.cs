namespace Snail.MCP.Roslyn.Domain;

internal sealed record CodeFix(
    string Title,
    string Provider,
    IReadOnlyList<FileEdit> Edits);
