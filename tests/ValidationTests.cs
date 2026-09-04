namespace Snail.MCP.Roslyn.Tests;

[Collection(nameof(AnalysisCollection))]
public sealed class ValidationTests(AnalysisFixture fixture)
{
    private static readonly string Candidate = Path.Combine(RepositoryPaths.Source, "Domain", "ValidationProbe.cs");

    [Fact]
    public async Task ValidateAsync_ContentsThatDoNotCompile_ReportsErrorsAndWritesNothing()
    {
        var result = await fixture.Validator.ValidateAsync(
            fixture.SolutionId,
            Candidate,
            "namespace Snail.MCP.Roslyn.Domain; public sealed class ValidationProbe { public int Broken => NoSuchMember; }",
            CancellationToken.None);

        Assert.False(result.Compiles);
        Assert.Contains(result.Introduced, issue => issue.Id == "CS0103");
        Assert.False(File.Exists(Candidate), "validation must never write to disk");
    }

    [Fact]
    public async Task ValidateAsync_ContentsThatCompile_ReportsCleanAndWritesNothing()
    {
        var result = await fixture.Validator.ValidateAsync(
            fixture.SolutionId,
            Candidate,
            "namespace Snail.MCP.Roslyn.Domain; public sealed class ValidationProbe { public int Fine => 1; }",
            CancellationToken.None);

        Assert.True(result.Compiles, string.Join("; ", result.Introduced.Select(issue => issue.Message)));
        Assert.False(File.Exists(Candidate), "validation must never write to disk");
    }

    [Fact]
    public async Task ValidateAsync_ChangeBreakingExistingFile_NamesWhatItWouldBreak()
    {
        var existing = Path.Combine(RepositoryPaths.Source, "Domain", "LineEdit.cs");

        var result = await fixture.Validator.ValidateAsync(
            fixture.SolutionId,
            existing,
            "namespace Snail.MCP.Roslyn.Domain; public sealed record LineEdit(int Line);",
            CancellationToken.None);

        Assert.False(result.Compiles);
        Assert.NotEmpty(result.Introduced);
    }
}
