namespace Snail.MCP.Roslyn.Domain;

/// <summary>A rename worked out by the compiler and handed back for the caller to apply.</summary>
public sealed record RenamePlan(
    string SymbolId,
    string OldName,
    string NewName,
    IReadOnlyList<FileEdit> Edits,
    IReadOnlyList<TextualMatch> Unhandled,
    IReadOnlyList<string> Conflicts,
    IReadOnlyList<SymbolMatch> Candidates,
    IReadOnlyList<string> Notes);
