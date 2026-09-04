namespace Snail.MCP.Roslyn.Tests;

[Collection(nameof(AnalysisCollection))]
public sealed class ReferenceTests(AnalysisFixture fixture)
{
    [Fact]
    public async Task FindAsync_PortUsedByTool_ReportsItsUses()
    {
        var page = await Find(nameof(ISolutionHost));

        Assert.True(page.Total > 1);
        Assert.Contains(page.Items, item => item.File.EndsWith("SolutionTools.cs", StringComparison.Ordinal));
    }

    [Fact]
    public async Task FindAsync_AnyResult_StatesScopeIsTheLoadedSolution()
    {
        var page = await Find(nameof(ISolutionHost));

        Assert.Contains(page.Notes, note => note.Contains("loaded solution", StringComparison.Ordinal));
    }

    [Fact]
    public async Task FindAsync_UnknownSymbolId_ReturnsNoItemsWithNote()
    {
        var page = await fixture.References.FindAsync(
            fixture.SolutionId, new SymbolQuery("T:Nowhere.NoSuchType", null, null, 0, 0), 50, CancellationToken.None);

        Assert.Empty(page.Items);
        Assert.Contains(page.Notes, note => note.Contains("search_symbols", StringComparison.Ordinal));
    }

    [Fact]
    public async Task FindAsync_AnyResult_PointsAtNoFileUnderObj()
    {
        var page = await Find(nameof(ISolutionHost));

        Assert.All(page.Items, item => Assert.DoesNotContain($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", item.File));
    }

    private async Task<ReferencePage> Find(string name)
    {
        var matches = await fixture.Symbols.SearchAsync(
            fixture.SolutionId, name, SymbolKindFilter.Type, 5, CancellationToken.None);
        var match = matches.Single(candidate => candidate.Name == name);

        return await fixture.References.FindAsync(
            fixture.SolutionId, new SymbolQuery(match.Id, null, null, 0, 0), 100, CancellationToken.None);
    }

    [Fact]
    public async Task FindAsync_CallSite_IsClassifiedAsInvocation()
    {
        var page = await fixture.References.FindAsync(
            fixture.SolutionId,
            await fixture.MemberAsync(nameof(ISolutionHost.LoadAsync), nameof(ISolutionHost)),
            200,
            CancellationToken.None);

        Assert.Contains(page.Items, reference => reference.Kind == "invocation");
    }

    [Fact]
    public async Task FindAsync_GenericRegistration_IsClassifiedAsTypeArgument()
    {
        var page = await fixture.References.FindAsync(
            fixture.SolutionId,
            await fixture.MemberAsync("RootPathMap", "Paths"),
            200,
            CancellationToken.None);

        Assert.Contains(page.Items, reference => reference.Kind == "type_argument");
    }
}
