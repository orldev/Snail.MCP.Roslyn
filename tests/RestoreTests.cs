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

    private const string WithoutDependencies = """{"project":{"frameworks":{"net10.0":{"dependencies":{}}}}}""";

    private const string WithNewtonsoft =
        """{"project":{"frameworks":{"net10.0":{"dependencies":{"Newtonsoft.Json":{"target":"Package","version":"[13.0.3, )"}}}}}}""";

    private readonly string _folder = TestFolder.Create();

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

    /// <summary>
    /// A repository on the artifacts layout keeps project.assets.json under one folder of its own, named
    /// after the project, rather than beside it. Measured 2026-10-15 on Snail.Toolkit.Web: sixteen
    /// restored projects were all called unrestored, so the agent heard that symbols do not bind while
    /// they did, and this server spent 687 ms restoring what needed nothing.
    /// </summary>
    [Fact]
    public async Task LoadAsync_ProjectRestoredIntoTheArtifactsFolder_IsNotCalledUnrestored()
    {
        await File.WriteAllTextAsync(Path.Combine(_folder, "Fresh.csproj"), WithPackage);
        await File.WriteAllTextAsync(Path.Combine(_folder, "Widget.cs"), Source);

        var assets = Path.Combine(_folder, "artifacts", "obj", "Fresh");

        Directory.CreateDirectory(assets);
        await File.WriteAllTextAsync(Path.Combine(assets, "project.assets.json"), "{}");

        using var fixture = new AnalysisFixture([new KeyValuePair<string, string?>("RestoreOnLoad", "false")]);
        var report = await fixture.Host.LoadAsync(_folder, CancellationToken.None);

        Assert.DoesNotContain(report.Notes, note => note.Contains("restore has not run", StringComparison.Ordinal));
        Assert.DoesNotContain(report.Notes, note => note.Contains("this server restored them", StringComparison.Ordinal));
    }

    /// <summary>
    /// The restore that ran here a release ago is not the restore of a checkout that adds a package: its assets miss the
    /// package, the compiler misses its references, and the solution was called Ready over a flood of errors about code
    /// that was fine (2026-10-10).
    /// </summary>
    [Fact]
    public async Task LoadAsync_PackageAddedAfterTheRestore_ReportsDegradedAndExplains()
    {
        await WriteRestoredAsync(Path.Combine(_folder, "obj"), WithoutDependencies);
        await File.WriteAllTextAsync(Path.Combine(_folder, "Fresh.csproj"), WithPackage);
        await File.WriteAllTextAsync(Path.Combine(_folder, "Widget.cs"), Source);

        using var fixture = new AnalysisFixture([new KeyValuePair<string, string?>("RestoreOnLoad", "false")]);
        var report = await fixture.Host.LoadAsync(_folder, CancellationToken.None);

        Assert.Equal(WorkspaceReadiness.Degraded, report.Readiness);
        Assert.Contains(report.Notes, note => note.Contains("restore has not run", StringComparison.Ordinal));
    }

    /// <summary>
    /// A restore that changes nothing rewrites none of its files, so a project edited after its restore is the common
    /// case: it is not unrestored while the assets hold every package it names.
    /// </summary>
    [Fact]
    public async Task LoadAsync_ProjectEditedAfterTheRestoreWithTheSamePackages_IsNotCalledUnrestored()
    {
        await WriteRestoredAsync(Path.Combine(_folder, "obj"), WithNewtonsoft);
        await File.WriteAllTextAsync(Path.Combine(_folder, "Fresh.csproj"), WithPackage);
        await File.WriteAllTextAsync(Path.Combine(_folder, "Widget.cs"), Source);

        using var fixture = new AnalysisFixture([new KeyValuePair<string, string?>("RestoreOnLoad", "false")]);
        var report = await fixture.Host.LoadAsync(_folder, CancellationToken.None);

        Assert.DoesNotContain(report.Notes, note => note.Contains("restore has not run", StringComparison.Ordinal));
    }

    /// <summary>
    /// The restore that runs on load decided by a check of its own, which looked beside each project alone: once the
    /// report stopped calling the artifacts layout unrestored, the server still restored it on every load.
    /// </summary>
    [Fact]
    public async Task LoadAsync_ProjectRestoredIntoTheArtifactsFolder_IsNotRestoredAgain()
    {
        await File.WriteAllTextAsync(Path.Combine(_folder, "Fresh.csproj"), WithPackage);
        await File.WriteAllTextAsync(Path.Combine(_folder, "Widget.cs"), Source);
        await WriteRestoredAsync(Path.Combine(_folder, "artifacts", "obj", "Fresh"), WithNewtonsoft);

        using var fixture = new AnalysisFixture();
        var report = await fixture.Host.LoadAsync(_folder, CancellationToken.None);

        Assert.DoesNotContain(report.Notes, note => note.Contains("restored them", StringComparison.Ordinal));
        Assert.DoesNotContain(report.Notes, note => note.Contains("restore has not run", StringComparison.Ordinal));
    }

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    /// <summary>An assets file restored a minute ago, before the project files the test writes next.</summary>
    private static async Task WriteRestoredAsync(string folder, string assets)
    {
        var file = Path.Combine(folder, "project.assets.json");

        Directory.CreateDirectory(folder);
        await File.WriteAllTextAsync(file, assets);
        File.SetLastWriteTimeUtc(file, DateTime.UtcNow.AddMinutes(-1));
    }
}
