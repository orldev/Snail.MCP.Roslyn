namespace Snail.MCP.Roslyn.Tests;

/// <summary>Loading reports the truth about what was analysed and what was not.</summary>
[Collection(nameof(AnalysisCollection))]
public sealed class SolutionLoadingTests(AnalysisFixture fixture)
{
    [Fact]
    public void Loading_this_repository_yields_a_semantic_workspace()
    {
        Assert.Equal(WorkspaceReadiness.Ready, fixture.Report.Readiness);
        Assert.NotEmpty(fixture.Report.Projects);
        Assert.All(fixture.Report.Projects, project => Assert.True(project.HasSemantics));
    }

    [Fact]
    public void A_project_without_metadata_references_is_never_reported_as_ready()
    {
        Assert.All(
            fixture.Report.Projects.Where(project => !project.HasSemantics),
            _ => Assert.NotEqual(WorkspaceReadiness.Ready, fixture.Report.Readiness));
    }

    [Fact]
    public async Task A_path_holding_no_project_fails_loudly_rather_than_returning_nothing()
    {
        var report = await fixture.Host.LoadAsync(Path.GetTempPath(), CancellationToken.None);

        Assert.Equal(WorkspaceReadiness.Failed, report.Readiness);
        Assert.NotEmpty(report.Notes);
    }

    [Fact]
    public async Task Unloading_releases_the_solution_and_says_that_further_questions_need_a_reload()
    {
        var folder = Directory.CreateTempSubdirectory("roslyn-unload").FullName;

        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(folder, "Tiny.csproj"),
                """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net10.0</TargetFramework>
                  </PropertyGroup>
                </Project>
                """);
            await File.WriteAllTextAsync(Path.Combine(folder, "Tiny.cs"), "namespace Tiny; public sealed class Thing;");

            var report = await fixture.Host.LoadAsync(folder, CancellationToken.None);
            var unloaded = fixture.Host.Unload(report.Id);
            var again = fixture.Host.Unload(report.Id);

            Assert.True(unloaded.Unloaded);
            Assert.False(again.Unloaded);
            Assert.Contains(unloaded.Notes, note => note.Contains("Load it again", StringComparison.Ordinal));

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                fixture.Diagnostics.ReadAsync(report.Id, null, IssueSeverity.Warning, 5, false, CancellationToken.None));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
