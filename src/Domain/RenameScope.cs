namespace Snail.MCP.Roslyn.Domain;

public sealed record RenameScope(
    bool RenamesOverloads = true,
    bool RenamesFile = true,
    bool SearchesComments = false,
    bool SearchesStrings = false);
