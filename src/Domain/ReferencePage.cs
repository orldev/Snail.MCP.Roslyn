using System.Text.Json.Serialization;

namespace Snail.MCP.Roslyn.Domain;

public sealed record ReferencePage(
    string SymbolId,
    IReadOnlyList<SymbolReference> Items,
    int Total,
    [property: JsonPropertyName("truncated")] bool IsTruncated,
    IReadOnlyList<string> Notes);
