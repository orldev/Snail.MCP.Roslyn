namespace Snail.MCP.Roslyn.Domain;

/// <summary>What the generators contributed to a project, so an unexplained symbol can be traced to its source.</summary>
public sealed record GeneratedCodePage(
    string Project,
    IReadOnlyList<string> Generators,
    IReadOnlyList<GeneratedFile> Files,
    int Total,
    bool Truncated,
    IReadOnlyList<string> Notes);
