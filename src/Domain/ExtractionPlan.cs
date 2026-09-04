namespace Snail.MCP.Roslyn.Domain;

/// <summary>While <see cref="Unresolved"/> is non-empty the plan carries no edits: they could not compile.
/// That list is the parameter list of the new component, waiting to be declared.</summary>
public sealed record ExtractionPlan(
    string Component,
    string? NewFile,
    IReadOnlyList<FileEdit> Edits,
    BlockReplacement? Replacement,
    IReadOnlyList<string> Unresolved,
    IReadOnlyList<string> Notes);
