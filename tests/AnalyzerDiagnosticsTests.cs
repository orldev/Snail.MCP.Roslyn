namespace Snail.MCP.Roslyn.Tests;

/// <summary>
/// get_diagnostics was empty while <c>dotnet build</c> failed on IDE0005 and VSTHRD103 (2026-10-17): the compiler had
/// nothing to say, the project's analyzers did, and its warnings were errors. An agent told to trust the answer instead
/// of the build trusted it.
/// </summary>
public sealed class AnalyzerDiagnosticsTests : IDisposable
{
    private const string Project =
        """
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <TargetFramework>net10.0</TargetFramework>
            <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
            <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
            <GenerateDocumentationFile>true</GenerateDocumentationFile>
          </PropertyGroup>
        </Project>
        """;

    private const string Rules =
        """
        root = true

        [*.cs]
        dotnet_diagnostic.IDE0005.severity = warning
        """;

    private const string Source =
        """
        using System.Text;

        namespace Styled;

        /// <summary>A widget.</summary>
        public static class Widget
        {
            /// <summary>Its name.</summary>
            public static string Name => "w";
        }
        """;

    private readonly string _folder = TestFolder.Create();

    [Fact]
    public async Task ReadAsync_WithoutAnalyzersWhereWarningsAreErrors_SaysTheyWereNotRun()
    {
        var (fixture, id, file) = await LoadAsync();

        using (fixture)
        {
            var page = await fixture.Diagnostics.ReadAsync(id, file, IssueSeverity.Warning, 50, false, false, CancellationToken.None);

            Assert.DoesNotContain(page.Items, issue => issue.Id == "IDE0005");
            Assert.Contains(page.Notes ?? [], note => note.Contains("analyzers were not run", StringComparison.Ordinal));
        }
    }

    [Fact]
    public async Task ReadAsync_WithAnalyzers_ReportsWhatFailsTheBuildAtItsSeverity()
    {
        var (fixture, id, file) = await LoadAsync();

        using (fixture)
        {
            var page = await fixture.Diagnostics.ReadAsync(id, file, IssueSeverity.Warning, 50, false, true, CancellationToken.None);

            Assert.Contains(page.Items, issue => issue.Id == "IDE0005" && issue.Severity == IssueSeverity.Error);
            Assert.DoesNotContain(page.Notes ?? [], note => note.Contains("analyzers were not run", StringComparison.Ordinal));
        }
    }

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private async Task<(AnalysisFixture Fixture, string Id, string File)> LoadAsync()
    {
        var file = Path.Combine(_folder, "Widget.cs");

        await File.WriteAllTextAsync(Path.Combine(_folder, "Styled.csproj"), Project);
        await File.WriteAllTextAsync(Path.Combine(_folder, ".editorconfig"), Rules);
        await File.WriteAllTextAsync(file, Source);

        var fixture = new AnalysisFixture();
        var report = await fixture.Host.LoadAsync(_folder, CancellationToken.None);

        return (fixture, report.Id, file);
    }
}
