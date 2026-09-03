namespace Snail.MCP.Roslyn.Domain;

/// <summary>A budgeted slice of diagnostics; truncation is always stated, never silent.</summary>
public sealed record IssuePage(
    IReadOnlyList<CodeIssue> Items,
    int Total,
    bool Truncated);
