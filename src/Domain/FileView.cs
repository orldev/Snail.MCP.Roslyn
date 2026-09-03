namespace Snail.MCP.Roslyn.Domain;

/// <summary>One file the way the compiler sees it: numbered text, its diagnostics, and the declarations it holds.</summary>
public sealed record FileView(
    string File,
    int TotalLines,
    int StartLine,
    int EndLine,
    string Text,
    IssuePage Issues,
    IReadOnlyList<FileSymbol> Symbols,
    IReadOnlyList<MarkupDirective> Directives,
    IReadOnlyList<string> Notes);
