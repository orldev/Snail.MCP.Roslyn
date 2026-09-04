namespace Snail.MCP.Roslyn.Domain;

public sealed record GeneratedFile(
    string Generator,
    string HintName,
    int Lines,
    string? Text);
