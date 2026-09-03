namespace Snail.MCP.Roslyn.Tests;

/// <summary>Container registrations are read from source, including the ones a rename cannot see.</summary>
[Collection(nameof(AnalysisCollection))]
public sealed class RegistrationTests(AnalysisFixture fixture)
{
    [Fact]
    public async Task Every_port_registered_by_this_server_is_reported_with_its_implementation()
    {
        var report = await fixture.Container.ReadAsync(fixture.SolutionId, null, 200, CancellationToken.None);

        var port = Assert.Single(report.Items, item => item.Service.EndsWith(nameof(IPathMap), StringComparison.Ordinal));

        Assert.Equal("Singleton", port.Lifetime);
        Assert.EndsWith("RootPathMap", port.Implementation);
        Assert.EndsWith("ServiceCollectionExtensions.cs", port.File, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_answer_warns_that_registrations_written_as_text_survive_a_rename()
    {
        var report = await fixture.Container.ReadAsync(fixture.SolutionId, null, 200, CancellationToken.None);

        Assert.Contains(report.Notes, note => note.Contains("rename_symbol", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Narrowing_by_service_name_keeps_only_the_matching_registrations()
    {
        var report = await fixture.Container.ReadAsync(fixture.SolutionId, "IScriptRunner", 200, CancellationToken.None);

        Assert.All(report.Items, item => Assert.Contains("IScriptRunner", item.Service, StringComparison.Ordinal));
    }
}
