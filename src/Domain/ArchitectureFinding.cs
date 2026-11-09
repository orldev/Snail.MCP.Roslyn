namespace Snail.MCP.Roslyn.Domain;

/// <summary>One breach of a structural rule, stated as a checkable claim about the code.</summary>
internal sealed record ArchitectureFinding(
    string Rule,
    string Message,
    string? File,
    int Line);
