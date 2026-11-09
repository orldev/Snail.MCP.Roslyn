namespace Snail.MCP.Roslyn.Domain;

internal sealed record GeneratedFile(
    string Generator,
    string HintName,
    int Lines,
    string? Text);
