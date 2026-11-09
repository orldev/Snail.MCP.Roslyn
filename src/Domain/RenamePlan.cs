namespace Snail.MCP.Roslyn.Domain;

internal sealed record RenamePlan(
    string SymbolId,
    string OldName,
    string NewName,
    IReadOnlyList<FileEdit> Edits,
    IReadOnlyList<TextualMatch> Unhandled,
    IReadOnlyList<string> Conflicts,
    IReadOnlyList<SymbolMatch> Candidates,
    IReadOnlyList<string> Notes);
