namespace Snail.MCP.Roslyn.Domain;

internal sealed record FileView(
    string File,
    int TotalLines,
    int StartLine,
    int EndLine,
    string Text,
    IssuePage Issues,
    IReadOnlyList<FileSymbol> Symbols,
    IReadOnlyList<MarkupDirective> Directives,
    IReadOnlyList<string> Notes);
