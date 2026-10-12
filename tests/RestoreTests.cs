namespace Snail.MCP.Roslyn.Tests;

/// <summary>
/// Nobody restored the packages of a working copy: the worker only clones it, and the server merely
/// noticed that restore had not run and said so in a note — to a model that could do nothing about it.
/// Since the server became a container with an SDK under it, it is the only one that needs it and can do it.
/// </summary>
public sealed class RestoreTests : IDisposable
{
    private const string WithPackage =
        """
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <TargetFramework>net10.0</TargetFramework>
          </PropertyGroup>
          <ItemGroup>
            <PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
          </ItemGroup>
        </Project>
        """;

    private const string WithoutPackages =
        """
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <TargetFramework>net10.0</TargetFramework>
          </PropertyGroup>
        </Project>
        """;

    private const string Source =
        """
        namespace Fresh;

        public sealed class Widget
        {
            public string Name { get; set; } = "w";
        }
        """;

    private readonly string _folder = Directory.CreateTempSubdirectory("roslyn-restore").FullName;

    /// <summary>A deployment that restores packages by other means says so in a setting — and hears the former explanation.</summary>
    [Fact]
    public async Task LoadAsync_ProjectWithoutRestore_ReportsDegradedAndExplains()
    {
        await File.WriteAllTextAsync(Path.Combine(_folder, "Unrestored.csproj"), WithPackage);
        await File.WriteAllTextAsync(Path.Combine(_folder, "Widget.cs"), Source);

        using var fixture = new AnalysisFixture([new KeyValuePair<string, string?>("RestoreOnLoad", "false")]);
        var report = await fixture.Host.LoadAsync(_folder, CancellationToken.None);

        Assert.Equal(WorkspaceReadiness.Degraded, report.Readiness);
        Assert.Contains(report.Notes, note => note.Contains("restore has not run", StringComparison.Ordinal));
        Assert.Contains(report.Notes, note => note.Contains("not about the code", StringComparison.Ordinal));
    }

    /// <summary>
    /// A project without a single package restores without the network: what is checked is that the server
    /// runs restore at all and says so, not that nuget.org answers.
    /// </summary>
    [Fact]
    public async Task LoadAsync_ProjectNeverRestored_RestoresItAndSaysSo()
    {
        await File.WriteAllTextAsync(Path.Combine(_folder, "Fresh.csproj"), WithoutPackages);
        await File.WriteAllTextAsync(Path.Combine(_folder, "Widget.cs"), Source);

        using var fixture = new AnalysisFixture();
        var report = await fixture.Host.LoadAsync(_folder, CancellationToken.None);

        Assert.Contains(report.Notes, note => note.Contains("this server restored them", StringComparison.Ordinal));
        Assert.True(File.Exists(Path.Combine(_folder, "obj", "project.assets.json")));
        Assert.Equal(WorkspaceReadiness.Ready, report.Readiness);
    }

    public void Dispose() => Directory.Delete(_folder, recursive: true);
}
