namespace Snail.MCP.Roslyn.Tests;

/// <summary>A workspace whose packages were never restored must say so, not report itself ready.</summary>
public sealed class RestoreTests : IDisposable
{
    private const string Project =
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

    private const string Source =
        """
        using Newtonsoft.Json;

        namespace Unrestored;

        public sealed class Widget
        {
            public string Name { get; set; } = "w";

            public string ToJson() => JsonConvert.SerializeObject(this);
        }
        """;

    private readonly string folder = Directory.CreateTempSubdirectory("roslyn-restore").FullName;

    [Fact]
    public async Task A_project_without_a_restore_is_reported_degraded_and_named()
    {
        await File.WriteAllTextAsync(Path.Combine(folder, "Unrestored.csproj"), Project);
        await File.WriteAllTextAsync(Path.Combine(folder, "Widget.cs"), Source);

        using var fixture = new AnalysisFixture();
        var report = await fixture.Host.LoadAsync(folder, CancellationToken.None);

        Assert.Equal(WorkspaceReadiness.Degraded, report.Readiness);
        Assert.Contains(report.Notes, note => note.Contains("restore has not run", StringComparison.Ordinal));
        Assert.Contains(report.Notes, note => note.Contains("not about the code", StringComparison.Ordinal));
    }

    public void Dispose() => Directory.Delete(folder, recursive: true);
}
