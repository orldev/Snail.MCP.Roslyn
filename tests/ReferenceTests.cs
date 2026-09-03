namespace Snail.MCP.Roslyn.Tests;

/// <summary>References answer what would break, and say where they stop looking.</summary>
[Collection(nameof(AnalysisCollection))]
public sealed class ReferenceTests(AnalysisFixture fixture)
{
    [Fact]
    public async Task A_port_used_by_a_tool_reports_its_uses()
    {
        var page = await Find(nameof(ISolutionHost));

        Assert.True(page.Total > 1);
        Assert.Contains(page.Items, item => item.File.EndsWith("SolutionTools.cs", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Every_answer_states_that_scope_is_the_loaded_solution()
    {
        var page = await Find(nameof(ISolutionHost));

        Assert.Contains(page.Notes, note => note.Contains("loaded solution", StringComparison.Ordinal));
    }

    [Fact]
    public async Task An_unknown_symbol_says_so_instead_of_returning_an_empty_list()
    {
        var page = await fixture.References.FindAsync(
            fixture.SolutionId, new SymbolQuery("T:Nowhere.NoSuchType", null, null, 0, 0), 50, CancellationToken.None);

        Assert.Empty(page.Items);
        Assert.Contains(page.Notes, note => note.Contains("search_symbols", StringComparison.Ordinal));
    }

    [Fact]
    public async Task No_reference_points_at_code_the_caller_cannot_edit()
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
    public async Task A_call_site_is_classified_as_an_invocation()
    {
        var page = await fixture.References.FindAsync(
            fixture.SolutionId,
            await fixture.MemberAsync(nameof(ISolutionHost.LoadAsync), nameof(ISolutionHost)),
            200,
            CancellationToken.None);

        Assert.Contains(page.Items, reference => reference.Kind == "invocation");
    }

    [Fact]
    public async Task A_generic_registration_is_classified_as_a_type_argument()
    {
        var page = await fixture.References.FindAsync(
            fixture.SolutionId,
            await fixture.MemberAsync("RootPathMap", "Paths"),
            200,
            CancellationToken.None);

        Assert.Contains(page.Items, reference => reference.Kind == "type_argument");
    }
}
