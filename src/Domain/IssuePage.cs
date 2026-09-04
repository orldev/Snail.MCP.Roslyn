namespace Snail.MCP.Roslyn.Domain;

public sealed record IssuePage(
    IReadOnlyList<CodeIssue> Items,
    int Total,
    bool Truncated);
