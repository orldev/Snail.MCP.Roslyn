namespace Snail.MCP.Roslyn.Domain;

/// <summary>A budgeted slice of analyzer findings, with the analyzers that actually ran.</summary>
public sealed record AnalyzerPage(
    IReadOnlyList<AnalyzerIssue> Items,
    IReadOnlyList<string> Analyzers,
    int Total,
    bool Truncated,
    long ElapsedMilliseconds,
    IReadOnlyList<string> Notes);
