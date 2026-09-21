namespace Snail.MCP.Roslyn.Tests;

/// <summary>
/// Восстановление пакетов на рабочей копии не делал никто: воркер её только клонирует, а сервер лишь
/// замечал, что restore не было, и писал это в примечании — модели, которая сделать с ним ничего не могла.
/// С тех пор как сервер стал контейнером с SDK под ним, он единственный, кому это и нужно, и кто это умеет.
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

    private readonly string folder = Directory.CreateTempSubdirectory("roslyn-restore").FullName;

    /// <summary>Развёртывание, которое восстанавливает пакеты иначе, говорит об этом настройкой — и слышит прежнее объяснение.</summary>
    [Fact]
    public async Task LoadAsync_ProjectWithoutRestore_ReportsDegradedAndExplains()
    {
        await File.WriteAllTextAsync(Path.Combine(folder, "Unrestored.csproj"), WithPackage);
        await File.WriteAllTextAsync(Path.Combine(folder, "Widget.cs"), Source);

        using var fixture = new AnalysisFixture([new KeyValuePair<string, string?>("Workspace:RestoreOnLoad", "false")]);
        var report = await fixture.Host.LoadAsync(folder, CancellationToken.None);

        Assert.Equal(WorkspaceReadiness.Degraded, report.Readiness);
        Assert.Contains(report.Notes, note => note.Contains("restore has not run", StringComparison.Ordinal));
        Assert.Contains(report.Notes, note => note.Contains("not about the code", StringComparison.Ordinal));
    }

    /// <summary>
    /// Проект без единого пакета восстанавливается без сети — проверяется именно то, что сервер вообще
    /// запускает restore и говорит об этом, а не то, что nuget.org отвечает.
    /// </summary>
    [Fact]
    public async Task LoadAsync_ProjectNeverRestored_RestoresItAndSaysSo()
    {
        await File.WriteAllTextAsync(Path.Combine(folder, "Fresh.csproj"), WithoutPackages);
        await File.WriteAllTextAsync(Path.Combine(folder, "Widget.cs"), Source);

        using var fixture = new AnalysisFixture();
        var report = await fixture.Host.LoadAsync(folder, CancellationToken.None);

        Assert.Contains(report.Notes, note => note.Contains("this server restored them", StringComparison.Ordinal));
        Assert.True(File.Exists(Path.Combine(folder, "obj", "project.assets.json")));
        Assert.Equal(WorkspaceReadiness.Ready, report.Readiness);
    }

    public void Dispose() => Directory.Delete(folder, recursive: true);
}
