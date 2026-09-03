namespace Snail.MCP.Roslyn.Domain;

/// <summary>What would break if a symbol changed, grouped so the blast radius is visible at a glance.</summary>
public sealed record ImpactReport(
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
