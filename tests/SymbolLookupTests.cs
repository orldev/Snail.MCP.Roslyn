namespace Snail.MCP.Roslyn.Tests;

/// <summary>Resolving a name to the identifier the other tools require.</summary>
[Collection(nameof(AnalysisCollection))]
public sealed class SymbolLookupTests(AnalysisFixture fixture)
{
    [Fact]
    public async Task Searching_a_known_type_returns_an_identifier_that_describes_back()
    {
        var matches = await fixture.Symbols.SearchAsync(
            fixture.SolutionId, nameof(RepositoryPaths), SymbolKindFilter.Type, 10, CancellationToken.None);

        var match = Assert.Single(matches, candidate => candidate.Name == nameof(RepositoryPaths));
        var details = await fixture.Symbols.DescribeAsync(
            fixture.SolutionId, new SymbolQuery(match.Id, null, null, 0, 0), CancellationToken.None);

        Assert.NotNull(details);
        Assert.Equal(match.Id, details.Id);
    }

    [Fact]
    public async Task An_empty_query_returns_nothing_instead_of_everything()
    {
        var matches = await fixture.Symbols.SearchAsync(
            fixture.SolutionId, "   ", SymbolKindFilter.Any, 10, CancellationToken.None);

        Assert.Empty(matches);
    }

    [Fact]
    public async Task A_position_inside_a_declaration_resolves_to_that_symbol()
    {
        var file = Path.Combine(RepositoryPaths.Source, "Domain", "IssueSeverity.cs");
        var line = File.ReadAllLines(file).Index().First(entry => entry.Item.Contains("enum IssueSeverity")).Index + 1;

        var details = await fixture.Symbols.DescribeAsync(
            fixture.SolutionId, new SymbolQuery(null, null, file, line, 13), CancellationToken.None);

        Assert.NotNull(details);
        Assert.Equal(nameof(IssueSeverity), details.Name);
    }

    [Fact]
    public async Task Reading_a_symbol_returns_its_own_declaration_and_nothing_else()
    {
        var matches = await fixture.Symbols.SearchAsync(
            fixture.SolutionId, nameof(SymbolSource), SymbolKindFilter.Type, 5, CancellationToken.None);
        var match = matches.Single(candidate => candidate.Name == nameof(SymbolSource));

        var source = await fixture.Symbols.ReadAsync(
            fixture.SolutionId, new SymbolQuery(match.Id, null, null, 0, 0), CancellationToken.None);

        Assert.NotNull(source);
        Assert.Contains("record SymbolSource", source.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("namespace", source.Text, StringComparison.Ordinal);
        Assert.True(source.EndLine >= source.StartLine);
    }
}
