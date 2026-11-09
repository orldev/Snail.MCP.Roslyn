using Snail.MCP.Roslyn.Adapters.Workspaces;

namespace Snail.MCP.Roslyn.Tests;

/// <summary>The strict policy exists to run the vendor's own analyzers and nobody else's. It is worth a test of
/// its own because it silently did neither: its idea of the SDK pointed at this process's runtime directory,
/// where no analyzer lives.</summary>
public sealed class AnalyzerPolicyTests : IDisposable
{
    private const string Project =
        """
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <TargetFramework>net10.0</TargetFramework>
          </PropertyGroup>
        </Project>
        """;

    private const string Source =
        """
        namespace Probe;

        public sealed class Widget
        {
            public string Describe() => "widget";
        }
        """;

    private readonly string _folder = TestFolder.Create();

    [Fact]
    public async Task RunAsync_StrictPolicy_RunsTheAnalyzersTheSdkShipsWithTheProject()
    {
        await File.WriteAllTextAsync(Path.Combine(_folder, "Probe.csproj"), Project);
        await File.WriteAllTextAsync(Path.Combine(_folder, "Widget.cs"), Source);

        using var strict = new AnalysisFixture(
            [new KeyValuePair<string, string?>("AnalyzerPolicy", "strict")]);

        var report = await strict.Host.LoadAsync(_folder, CancellationToken.None);
        var page = await strict.Analyzers.RunAsync(report.Id, null, null, IssueSeverity.Info, 50, CancellationToken.None);

        Assert.NotEmpty(page.Analyzers);

        Assert.DoesNotContain(page.Notes, note => note.Contains("were not run", StringComparison.Ordinal));
        Assert.DoesNotContain(page.Notes, note => note.Contains("no analyzers of its own", StringComparison.Ordinal));
    }

    /// <summary>
    /// The image mounts its package cache at /nuget and says so in NUGET_PACKAGES. The guard looked in the user's home
    /// alone, so in a container every analyzer a project restored was refused as one from outside.
    /// </summary>
    [Fact]
    public void Allows_AnalyzerInTheCacheNuGetPackagesNames_RunsIt()
    {
        var cache = AnalyzerGuard.CacheOf(_folder);
        var analyzer = Path.Combine(_folder, "some.analyzers", "1.0.0", "analyzers", "dotnet", "cs", "Some.Analyzers.dll");

        Assert.True(AnalyzerGuard.Allows("strict", analyzer, null, cache));
    }

    [Fact]
    public void CacheOf_NoVariable_IsTheOneInTheUsersHome() =>
        Assert.Equal(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages"),
            AnalyzerGuard.CacheOf(null));

    [Fact]
    public void Allows_AnalyzerOutsideEveryTrustedFolder_IsRefusedUnderStrict() =>
        Assert.False(AnalyzerGuard.Allows("strict", Path.Combine(_folder, "elsewhere", "Evil.dll"), null, AnalyzerGuard.CacheOf(null)));

    public void Dispose() => Directory.Delete(_folder, recursive: true);
}
