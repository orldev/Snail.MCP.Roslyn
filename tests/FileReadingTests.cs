using Snail.MCP.Roslyn.Adapters.Workspaces;
using Snail.Sdk.Mcp.Hosting.Answers;

namespace Snail.MCP.Roslyn.Tests;

[Collection(nameof(AnalysisCollection))]
public sealed class FileReadingTests(AnalysisFixture fixture)
{
    private const string Broken =
        """
        namespace Snail.MCP.Roslyn;

        internal sealed class ReadWithAnError
        {
            public int Broken() => undefinedName;
        }
        """;

    [Fact]
    public async Task ReadAsync_SourceFileOfSolution_ReturnsWholeNumberedTextWithoutIssues()
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
    public async Task ReadAsync_SourceFile_ReturnsOutlineIdsThatFindReferencesAccepts()
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
    public async Task ReadAsync_LineWindow_ReturnsExactlyTheRequestedLines()
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
    public async Task ReadAsync_FileOutsideSolution_ReturnsTextOnlyWithNote()
    {
        var file = Path.Combine(RepositoryPaths.Root, "README.md");

        var view = await fixture.Reader.ReadAsync(fixture.SolutionId, file, 1, 2000, CancellationToken.None);

        Assert.NotEmpty(view.Text);
        Assert.Empty(view.Symbols);
        Assert.Empty(view.Issues.Items);
        Assert.Contains(view.Notes, note => note.Contains("not a C# document", StringComparison.Ordinal));
    }

    /// <remarks>The issue's line has to index the text this very answer returned; they come from one
    /// snapshot.</remarks>
    [Fact]
    public async Task ReadAsync_FileWithACompilerError_ReportsItAgainstTheTextItReturns()
    {
        var file = Leftovers.Unreadable;

        try
        {
            await File.WriteAllTextAsync(file, Broken, CancellationToken.None);

            var view = await fixture.Reader.ReadAsync(fixture.SolutionId, file, 1, 2000, CancellationToken.None);

            var error = Assert.Single(view.Issues.Items, issue => issue.Id == "CS0103");

            Assert.Equal(file, error.File);

            Assert.Contains("undefinedName", view.Text.Split('\n')[error.Line - 1], StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public async Task ReadAsync_MissingFile_IsRefusedAsNotFound()
    {
        var file = Path.Combine(RepositoryPaths.Source, "DoesNotExist.cs");

        var refused = await Assert.ThrowsAsync<ToolRefusalException>(() =>
            fixture.Reader.ReadAsync(fixture.SolutionId, file, 1, 2000, CancellationToken.None));

        Assert.Equal(RefusalTypes.NotFound, refused.Type);
        Assert.Contains("DoesNotExist.cs", refused.Error, StringComparison.Ordinal);
    }
}
