using System.Text.Json.Serialization;

namespace Snail.MCP.Roslyn.Domain;

public sealed record ArchitectureReport(
    IReadOnlyList<ArchitectureFinding> Findings,
    int Total,
    [property: JsonPropertyName("truncated")] bool IsTruncated,
    IReadOnlyList<string> Notes);
