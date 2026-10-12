using System.Text.Json.Serialization;

namespace Snail.MCP.Roslyn.Domain;

public sealed record IssuePage(
    IReadOnlyList<CodeIssue> Items,
    int Total,
    [property: JsonPropertyName("truncated")] bool IsTruncated);
