namespace Snail.MCP.Roslyn.Domain;

/// <summary>One offered repair for a diagnostic, expressed as edits the caller applies.</summary>
public sealed record CodeFix(
    string Title,
    string Provider,
    IReadOnlyList<FileEdit> Edits);
