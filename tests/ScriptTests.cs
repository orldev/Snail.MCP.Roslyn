using System.Diagnostics;

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
        Assert.Equal("3", result.Value);
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

    /// <summary>A script that ignores cancellation cannot be stopped, so the promise the timeout makes is that
    /// the caller gets an answer — not that the script ends.</summary>
    [Fact]
    public async Task RunAsync_ScriptIgnoringCancellation_AnswersAtTheTimeoutAndSaysItRunsOn()
    {
        using var impatient = new AnalysisFixture(
            [new KeyValuePair<string, string?>("ScriptTimeoutSeconds", "1")]);

        await impatient.InitializeAsync();

        var started = Stopwatch.GetTimestamp();

        var result = await impatient.Scripts.RunAsync(
            impatient.SolutionId,
            "System.Threading.Thread.Sleep(30000); 1",
            CancellationToken.None);

        var waited = Stopwatch.GetElapsedTime(started);

        Assert.False(result.Succeeded);
        Assert.True(waited < TimeSpan.FromSeconds(10), $"the call itself hung for {waited.TotalSeconds:0.0} s");
        Assert.Contains(result.Errors, error => error.Contains("did not stop when asked", StringComparison.Ordinal));
        Assert.Contains(result.Notes, note => note.Contains("still running", StringComparison.Ordinal));
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
