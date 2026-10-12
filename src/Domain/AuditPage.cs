using System.Text.Json.Serialization;

namespace Snail.MCP.Roslyn.Domain;

public sealed record AuditPage(
    IReadOnlyList<AuditFinding> Items,
    int Total,
    [property: JsonPropertyName("truncated")] bool IsTruncated,
    IReadOnlyList<string> Notes);
