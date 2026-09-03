namespace Snail.MCP.Roslyn.Domain;

/// <summary>A compiler diagnostic located in source the caller can edit.</summary>
public sealed record CodeIssue(
    string Id,
    IssueSeverity Severity,
    string Message,
    string? File,
    int Line,
    int Column,
    int Repeats);
