namespace Snail.MCP.Roslyn.Domain;

public sealed record AnalyzerPage(
    IReadOnlyList<AnalyzerIssue> Items,
    IReadOnlyList<string> Analyzers,
    int Total,
    bool Truncated,
    long ElapsedMilliseconds,
    IReadOnlyList<string> Notes);
