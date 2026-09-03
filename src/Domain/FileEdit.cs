namespace Snail.MCP.Roslyn.Domain;

/// <summary>Every change a refactoring makes to one file, including a rename of the file itself.</summary>
/// <remarks>Line edits carry the change when the layout survives it; otherwise <see cref="Content"/> carries the whole file.</remarks>
public sealed record FileEdit(
    string File,
    string? RenamedTo,
    IReadOnlyList<LineEdit> Lines,
    string? Content = null);
