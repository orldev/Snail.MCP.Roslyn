namespace Snail.MCP.Roslyn.Tests;

[Collection(nameof(AnalysisCollection))]
public sealed class SymbolLookupTests(AnalysisFixture fixture)
{
    [Fact]
    public async Task SearchAsync_KnownType_ReturnsIdThatDescribeAsyncResolves()
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
    public async Task SearchAsync_BlankQuery_ReturnsEmpty()
    {
        var matches = await fixture.Symbols.SearchAsync(
            fixture.SolutionId, "   ", SymbolKindFilter.Any, 10, CancellationToken.None);

        Assert.Empty(matches);
    }

    [Fact]
    public async Task DescribeAsync_PositionInsideDeclaration_ResolvesToThatSymbol()
    {
        var file = Path.Combine(RepositoryPaths.Source, "Domain", "IssueSeverity.cs");
        var line = File.ReadAllLines(file).Index().First(entry => entry.Item.Contains("enum IssueSeverity")).Index + 1;

        var details = await fixture.Symbols.DescribeAsync(
            fixture.SolutionId, new SymbolQuery(null, null, file, line, 13), CancellationToken.None);

        Assert.NotNull(details);
        Assert.Equal(nameof(IssueSeverity), details.Name);
    }

    [Fact]
    public async Task ReadAsync_TypeSymbol_ReturnsDeclarationWithoutSurroundingCode()
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
