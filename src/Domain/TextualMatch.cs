namespace Snail.MCP.Roslyn.Domain;

/// <summary>A textual occurrence the compiler does not treat as a reference, left for a human to judge.</summary>
public sealed record TextualMatch(
    string File,
    int Line,
    string Snippet,
    string Reason);
