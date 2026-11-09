namespace Snail.MCP.Roslyn.Tests;

/// <summary>A folder called bin or obj inside the sources is still the author's: the compiler builds what is in
/// it, so hiding its errors hides errors the caller has to fix.</summary>
public sealed class OutputPathTests : IDisposable
{
    private const string Project =
        """
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <TargetFramework>net10.0</TargetFramework>
          </PropertyGroup>
        </Project>
        """;

    private const string Broken =
        """
        namespace Probe.Features.Bin;

        public sealed class Item
        {
            public int Broken() => undefinedName;
        }
        """;

    private readonly string _folder = TestFolder.Create();

    [Fact]
    public async Task ReadAsync_FileUnderAFolderNamedBinInsideTheSources_ReportsItsErrors()
    {
        var authored = Path.Combine(_folder, "Features", "bin");
        Directory.CreateDirectory(authored);

        await File.WriteAllTextAsync(Path.Combine(_folder, "Probe.csproj"), Project);
        await File.WriteAllTextAsync(Path.Combine(authored, "Item.cs"), Broken);

        using var fixture = new AnalysisFixture();
        var report = await fixture.Host.LoadAsync(_folder, CancellationToken.None);

        var page = await fixture.Diagnostics.ReadAsync(
            report.Id, null, IssueSeverity.Error, 50, false, CancellationToken.None);

        Assert.Contains(page.Items, issue => issue.File?.EndsWith("Item.cs", StringComparison.Ordinal) == true);
    }

    public void Dispose() => Directory.Delete(_folder, recursive: true);
}
