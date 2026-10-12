using System.Text.Json.Serialization;

namespace Snail.MCP.Roslyn.Domain;

public sealed record UnloadReport(string Id, [property: JsonPropertyName("unloaded")] bool WasUnloaded, IReadOnlyList<string> Notes);
