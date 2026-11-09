namespace Snail.MCP.Roslyn.Domain;

internal sealed record ImpactReport(
    string SymbolId,
    string Name,
    string Kind,
    string Accessibility,
    bool CrossesProjects,
    int References,
    IReadOnlyList<ImpactSite> Projects,
    IReadOnlyList<SymbolMatch> Overrides,
    IReadOnlyList<SymbolMatch> Implementations,
    IReadOnlyList<TextualMatch> Unhandled,
    IReadOnlyList<string> Notes);
