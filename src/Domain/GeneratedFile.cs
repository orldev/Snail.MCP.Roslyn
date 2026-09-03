namespace Snail.MCP.Roslyn.Domain;

/// <summary>One file a source generator produced, which no caller may edit.</summary>
public sealed record GeneratedFile(
    string Generator,
    string HintName,
    int Lines,
    string? Text);
