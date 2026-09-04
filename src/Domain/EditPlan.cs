namespace Snail.MCP.Roslyn.Domain;

/// <summary>What a refactoring proposes. <see cref="Replacements"/> carries the changes that touch a region of a
/// file, <see cref="Edits"/> only the files whose whole content is the payload, such as one being created.</summary>
public sealed record EditPlan(
    string Operation,
    string Target,
    IReadOnlyList<FileEdit> Edits,
    IReadOnlyList<BlockReplacement> Replacements,
    IReadOnlyList<TextualMatch> Unhandled,
    IReadOnlyList<string> Conflicts,
    IReadOnlyList<SymbolMatch> Candidates,
    IReadOnlyList<string> Notes);
