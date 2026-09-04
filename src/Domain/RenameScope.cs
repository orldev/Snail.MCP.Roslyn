namespace Snail.MCP.Roslyn.Domain;

public sealed record RenameScope(
    bool RenameOverloads = true,
    bool RenameFile = true,
    bool SearchComments = false,
    bool SearchStrings = false);
