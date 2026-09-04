namespace Snail.MCP.Roslyn.Domain;

public sealed record CodeFix(
    string Title,
    string Provider,
    IReadOnlyList<FileEdit> Edits);
