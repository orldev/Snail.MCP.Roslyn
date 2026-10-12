using System.Text.Json.Serialization;

namespace Snail.MCP.Roslyn.Domain;

public sealed record AnalyzerPage(
    IReadOnlyList<AnalyzerIssue> Items,
    IReadOnlyList<string> Analyzers,
    int Total,
    [property: JsonPropertyName("truncated")] bool IsTruncated,
    long ElapsedMilliseconds,
    IReadOnlyList<string> Notes);
