namespace Snail.MCP.Roslyn.Domain;

/// <summary>A diagnostic from an analyzer rather than the compiler, carrying the category that explains why it matters.</summary>
internal sealed record AnalyzerIssue(
    string Id,
    string Category,
    IssueSeverity Severity,
    string Message,
    string? File,
    int Line,
    int Column,
    int Repeats);
