namespace Snail.MCP.Roslyn.Domain;

/// <summary>Line edits carry the change when the layout survives it; otherwise <see cref="Content"/> carries the whole file.</summary>
public sealed record FileEdit(
    string File,
    string? RenamedTo,
    IReadOnlyList<LineEdit> Lines,
    string? Content = null);
