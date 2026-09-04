namespace Snail.MCP.Roslyn.Tests;

[Collection(nameof(AnalysisCollection))]
public sealed class ScriptTests(AnalysisFixture fixture)
{
    [Fact]
    public async Task RunAsync_ScriptReadingSolution_SeesLoadedProjects()
    {
        var result = await fixture.Scripts.RunAsync(
            fixture.SolutionId,
            "Solution.Projects.Count()",
            CancellationToken.None);

        Assert.True(result.Succeeded, string.Join("; ", result.Errors));
        Assert.Equal("2", result.Value);
    }

    [Fact]
    public async Task RunAsync_ScriptAggregatingTypes_AnswersOverWholeSolution()
    {
        var result = await fixture.Scripts.RunAsync(
            fixture.SolutionId,
            "Types(\"Roslyn\").Count(t => t.DeclaredAccessibility == Accessibility.Internal)",
            CancellationToken.None);

        Assert.True(result.Succeeded, string.Join("; ", result.Errors));
        Assert.True(int.Parse(result.Value!) > 5, $"expected the internal adapters, saw {result.Value}");
    }

    [Fact]
    public async Task RunAsync_ScriptThatDoesNotCompile_ReturnsDiagnosticsAndAvailableGlobals()
    {
        var result = await fixture.Scripts.RunAsync(fixture.SolutionId, "NoSuchGlobal.Whatever()", CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.NotEmpty(result.Errors);
        Assert.Contains(result.Notes, note => note.Contains("Globals available", StringComparison.Ordinal));
    }
}
