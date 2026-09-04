namespace Snail.MCP.Roslyn.Domain;

public sealed record AuditPage(
    IReadOnlyList<AuditFinding> Items,
    int Total,
    bool Truncated,
    IReadOnlyList<string> Notes);
