namespace Snail.MCP.Roslyn.Domain;

/// <summary>A budgeted slice of audit findings; truncation is always stated.</summary>
public sealed record AuditPage(
    IReadOnlyList<AuditFinding> Items,
    int Total,
    bool Truncated,
    IReadOnlyList<string> Notes);
