namespace Snail.MCP.Roslyn.Domain;

/// <summary>What to lift out of a markup file and what judgment the caller supplies: renames of page-private
/// names, the @code block of the new component, and the tag that takes the block's place.</summary>
internal sealed record ExtractionRequest(
    string File,
    int StartLine,
    int EndLine,
    string Name,
    IReadOnlyDictionary<string, string>? Substitutions,
    string? Code,
    string? Tag,
    string? NewFile);
