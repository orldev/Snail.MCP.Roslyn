using System.Text.Json.Serialization;

namespace Snail.MCP.Roslyn.Domain;

public sealed record GeneratedCodePage(
    string Project,
    IReadOnlyList<string> Generators,
    IReadOnlyList<GeneratedFile> Files,
    int Total,
    [property: JsonPropertyName("truncated")] bool IsTruncated,
    IReadOnlyList<string> Notes);
