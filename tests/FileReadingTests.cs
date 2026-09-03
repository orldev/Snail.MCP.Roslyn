namespace Snail.MCP.Roslyn.Tests;

/// <summary>Reading one file with everything the compiler knows about it.</summary>
[Collection(nameof(AnalysisCollection))]
public sealed class FileReadingTests(AnalysisFixture fixture)
{
    [Fact]
    public async Task A_source_file_arrives_numbered_whole_and_clean()
    {
        var file = Path.Combine(RepositoryPaths.Source, "Adapters", "Workspaces", "SymbolResolver.cs");

        var view = await fixture.Reader.ReadAsync(fixture.SolutionId, file, 1, 2000, CancellationToken.None);

        Assert.Equal(1, view.StartLine);
        Assert.Equal(view.TotalLines, view.EndLine);
        Assert.Contains("internal sealed class SymbolResolver", view.Text, StringComparison.Ordinal);
        Assert.Matches(@"^\s+1\t", view.Text);
        Assert.Empty(view.Issues.Items);
        Assert.NotEmpty(view.Notes);
    }

    [Fact]
    public async Task The_outline_carries_identifiers_that_feed_find_references_directly()
    {
        var file = Path.Combine(RepositoryPaths.Source, "Adapters", "Workspaces", "SymbolFacts.cs");

        var view = await fixture.Reader.ReadAsync(fixture.SolutionId, file, 1, 2000, CancellationToken.None);

        var method = Assert.Single(view.Symbols, symbol => symbol.Name == "IdOf");

        Assert.Equal("Method", method.Kind);
        Assert.True(method.Line > 0);
        Assert.True(method.References > 0, "IdOf is called across the solution, so its count cannot be zero.");

        var references = await fixture.References.FindAsync(
            fixture.SolutionId, new SymbolQuery(method.Id, null, null, 0, 0), 10, CancellationToken.None);

        Assert.True(references.Total > 0);
    }

    [Fact]
    public async Task A_window_returns_exactly_the_lines_asked_for()
    {
        var file = Path.Combine(RepositoryPaths.Source, "Adapters", "Workspaces", "SymbolResolver.cs");

        var view = await fixture.Reader.ReadAsync(fixture.SolutionId, file, 10, 5, CancellationToken.None);

        Assert.Equal(10, view.StartLine);
        Assert.Equal(14, view.EndLine);
        Assert.Equal(5, view.Text.Split('\n').Length);
        Assert.Matches(@"^\s+10\t", view.Text);
        Assert.True(view.TotalLines > view.EndLine);
    }

    [Fact]
    public async Task A_file_outside_the_solution_returns_text_alone_and_says_so()
    {
        var file = Path.Combine(RepositoryPaths.Source, "README.md");

        var view = await fixture.Reader.ReadAsync(fixture.SolutionId, file, 1, 2000, CancellationToken.None);

        Assert.NotEmpty(view.Text);
        Assert.Empty(view.Symbols);
        Assert.Empty(view.Issues.Items);
        Assert.Contains(view.Notes, note => note.Contains("not a C# document", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_missing_file_is_an_error_not_an_empty_answer()
    {
        var file = Path.Combine(RepositoryPaths.Source, "DoesNotExist.cs");

        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            fixture.Reader.ReadAsync(fixture.SolutionId, file, 1, 2000, CancellationToken.None));
    }
}
