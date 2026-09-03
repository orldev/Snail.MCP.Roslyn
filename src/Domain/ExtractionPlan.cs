namespace Snail.MCP.Roslyn.Domain;

/// <summary>A plan to lift a markup block into a new component: the new file, the compact page-side
/// replacement, and the names that do not resolve inside the lifted block — the parameter list waiting
/// to be declared. While that list is non-empty the plan carries no edits: they could not compile.</summary>
public sealed record ExtractionPlan(
    string Component,
    string? NewFile,
    IReadOnlyList<FileEdit> Edits,
    BlockReplacement? Replacement,
    IReadOnlyList<string> Unresolved,
    IReadOnlyList<string> Notes);

/// <summary>One compact edit: delete lines StartLine through EndLine of File and put Text in their place.</summary>
public sealed record BlockReplacement(
    string File,
    int StartLine,
    int EndLine,
    string Text);
