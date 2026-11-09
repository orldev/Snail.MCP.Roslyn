using System.Text.Json.Serialization;

namespace Snail.MCP.Roslyn.Domain;

internal sealed record UnloadReport(string Id, [property: JsonPropertyName("unloaded")] bool WasUnloaded, IReadOnlyList<string> Notes);
