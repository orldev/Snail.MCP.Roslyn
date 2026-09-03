namespace Snail.MCP.Roslyn.Domain;

/// <summary>One line replaced by a refactoring, kept verbatim so applying it preserves layout.</summary>
public sealed record LineEdit(int Line, string Before, string After);
