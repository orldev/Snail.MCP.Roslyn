using System.Text.Json.Serialization;

namespace Snail.MCP.Roslyn.Domain;

internal sealed record RegistrationReport(
    IReadOnlyList<Registration> Items,
    IReadOnlyList<SymbolMatch> Unregistered,
    int Total,
    [property: JsonPropertyName("truncated")] bool IsTruncated,
    IReadOnlyList<string> Notes);
