namespace Snail.MCP.Roslyn.Domain;

internal sealed record RenameScope(
    bool RenamesOverloads = true,
    bool RenamesFile = true,
    bool SearchesComments = false,
    bool SearchesStrings = false);
