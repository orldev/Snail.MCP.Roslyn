namespace Snail.MCP.Roslyn.Domain;

/// <summary>One Razor directive a markup file declares, such as @page, @inject or @inherits.</summary>
public sealed record MarkupDirective(
    string Text,
    int Line);
