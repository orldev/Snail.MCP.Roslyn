namespace Snail.MCP.Roslyn.Domain;

/// <summary>What the compiler would say about a file's proposed contents, before it reaches the disk.</summary>
internal sealed record ValidationResult(
    bool Compiles,
    string File,
    IReadOnlyList<CodeIssue> Introduced,
    IReadOnlyList<CodeIssue> Resolved,
    IReadOnlyList<string> Notes);
