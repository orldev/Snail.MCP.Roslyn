namespace Snail.MCP.Roslyn.Domain;

/// <summary>A structural refactoring worked out by the compiler and handed back for the caller to apply.</summary>
public sealed record EditPlan(
    string Operation,
    string Target,
    IReadOnlyList<FileEdit> Edits,
    IReadOnlyList<TextualMatch> Unhandled,
    IReadOnlyList<string> Conflicts,
    IReadOnlyList<SymbolMatch> Candidates,
    IReadOnlyList<string> Notes);
