namespace Snail.MCP.Roslyn.Domain;

/// <summary>What a rename is allowed to touch besides the symbol itself.</summary>
public sealed record RenameScope(
    bool RenameOverloads = true,
    bool RenameFile = true,
    bool SearchComments = false,
    bool SearchStrings = false);
