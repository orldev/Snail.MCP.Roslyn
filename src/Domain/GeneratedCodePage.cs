namespace Snail.MCP.Roslyn.Domain;

public sealed record GeneratedCodePage(
    string Project,
    IReadOnlyList<string> Generators,
    IReadOnlyList<GeneratedFile> Files,
    int Total,
    bool Truncated,
    IReadOnlyList<string> Notes);
