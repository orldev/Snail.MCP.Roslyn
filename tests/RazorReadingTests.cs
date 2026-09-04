using System.Diagnostics;

namespace Snail.MCP.Roslyn.Tests;

public sealed class RazorReadingTests : IAsyncLifetime, IDisposable
{
    private const string Project =
        """
        <Project Sdk="Microsoft.NET.Sdk.Razor">
          <PropertyGroup>
            <TargetFramework>net10.0</TargetFramework>
            <Nullable>enable</Nullable>
            <ImplicitUsings>enable</ImplicitUsings>
          </PropertyGroup>
          <ItemGroup>
            <PackageReference Include="Microsoft.AspNetCore.Components.Web" Version="10.0.0" />
          </ItemGroup>
        </Project>
        """;

    private const string Imports =
        """
        @using Microsoft.AspNetCore.Components
        @using Microsoft.AspNetCore.Components.Web
        """;

    private const string Badge =
        """
        <span class="badge">@Label (@Count)</span>

        @code {
            [Parameter]
            public string? Label { get; set; }

            [Parameter]
            public int Count { get; set; }

            [Parameter]
            public string? Ghost { get; set; }

            public int Doubled() => Count * 2;
        }
        """;

    private const string Panel =
        """
        @page "/panel"
        @inject NavigationManager Navigation
        @inject System.IServiceProvider Services

        <h1>Panel</h1>

        <Badge Label="item" Count="3" />

        @code {
            private void Go() => Navigation.NavigateTo("/");
        }
        """;

    private const string Board =
        """
        @foreach (var item in items)
        {
            <button @onclick="() => Pick(item)">@item</button>
        }

        @foreach (var item in items)
        {
            <Badge @key="item" Label="@item" Count="1" />
        }

        <CascadingValue Value="items">
            <Badge Label="x" Count="2" />
        </CascadingValue>

        @code {
            private readonly string[] items = ["a", "b"];

            private void Pick(string item) => _ = item;
        }
        """;

    private const string Cards =
        """
        <div class="card">
            <h2>@LeftTitle</h2>
            <Badge Label="left" Count="1" />
        </div>

        <div class="card">
            <h2>@RightTitle</h2>
            <Badge Label="right" Count="2" />
        </div>

        @code {
            private string LeftTitle => "l";
            private string RightTitle => "r";
        }
        """;

    private const string Wide =
        """
        @* <Badge Label="ghost" Count="0" /> *@

        <Badge Label="wide"
               Count="9" />
        """;

    private const string Rows =
        """
        @foreach (var item in items)
        {
            <Badge @key="item"
                   Label="@item"
                   Count="1"
                   CountChanged="@(value => Pick(value))" />
        }

        @code {
            private readonly string[] items = ["a", "b"];

            private void Pick(int value) => _ = value;
        }
        """;

    private const string RootTile =
        """
        <span>root</span>

        @code {
            public int RootOnly { get; set; }
        }
        """;

    private const string NestedTile =
        """
        <span>nested</span>

        @code {
            public int NestedOnly { get; set; }
        }
        """;

    private const string Modern =
        """
        @rendermode InteractiveServer

        @* <Badge Label="ghost" Count="0" /> *@

        <Badge Label="modern" Count="1" />
        """;

    private const string Quoted =
        """
        <Badge Label="write </Badge> to close it" Count="1" />

        <Badge Count="2">
            <span>real</span>
        </Badge>
        """;

    private const string Braces =
        """
        @foreach (var item in items)
        {
            <p>type { to open a block</p>
            <li>@item</li>
        }

        <button @onclick="() => Pick(1)">outside every loop</button>

        @code {
            private readonly string[] items = ["a"];

            private void Pick(int value) => _ = value;
        }
        """;

    private readonly string folder = Directory.CreateTempSubdirectory("roslyn-razor").FullName;

    private readonly AnalysisFixture fixture = new();

    private string solutionId = string.Empty;

    public async Task InitializeAsync()
    {
        await File.WriteAllTextAsync(Path.Combine(folder, "Blazing.csproj"), Project);
        await File.WriteAllTextAsync(Path.Combine(folder, "_Imports.razor"), Imports);
        await File.WriteAllTextAsync(Path.Combine(folder, "Badge.razor"), Badge);
        await File.WriteAllTextAsync(Path.Combine(folder, "Panel.razor"), Panel);
        await File.WriteAllTextAsync(Path.Combine(folder, "Board.razor"), Board);
        await File.WriteAllTextAsync(Path.Combine(folder, "Cards.razor"), Cards);
        await File.WriteAllTextAsync(Path.Combine(folder, "Wide.razor"), Wide);
        await File.WriteAllTextAsync(Path.Combine(folder, "Rows.razor"), Rows);
        await File.WriteAllTextAsync(Path.Combine(folder, "Quoted.razor"), Quoted);
        await File.WriteAllTextAsync(Path.Combine(folder, "Braces.razor"), Braces);

        // Same leaf name in two folders: the generated hint names differ only by the directory prefix.
        var nested = Directory.CreateDirectory(Path.Combine(folder, "Components", "Pages")).FullName;
        await File.WriteAllTextAsync(Path.Combine(folder, "Modern.razor"), Modern);
        await File.WriteAllTextAsync(Path.Combine(folder, "Tile.razor"), RootTile);
        await File.WriteAllTextAsync(Path.Combine(nested, "Tile.razor"), NestedTile);

        await RestoreAsync();

        var report = await fixture.Host.LoadAsync(folder, CancellationToken.None);
        solutionId = report.Id;
    }

    [Fact]
    public async Task ReadAsync_ComponentWithCodeBlock_MapsDeclarationsToAuthoredLinesWithUseCounts()
    {
        var view = await fixture.Reader.ReadAsync(
            solutionId, Path.Combine(folder, "Badge.razor"), 1, 2000, CancellationToken.None);

        var component = Assert.Single(view.Symbols, symbol => symbol.Name == "Badge" && symbol.Kind == "NamedType");
        Assert.Equal(0, component.Line);
        Assert.True(component.References > 0, "Panel's markup uses <Badge />, so the component's count cannot be zero.");

        var label = Assert.Single(view.Symbols, symbol => symbol.Name == "Label");
        Assert.Equal("Property", label.Kind);
        Assert.Equal(5, label.Line);
        Assert.True(label.References > 0, "Panel's markup assigns Label, so its count cannot be zero.");

        Assert.Single(view.Symbols, symbol => symbol.Name == "Doubled" && symbol.Kind == "Method");
        Assert.Contains(view.Notes, note => note.Contains("mapped back", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ReadAsync_ComponentMarkup_ReturnsComponentTagsAndDirectives()
    {
        var view = await fixture.Reader.ReadAsync(
            solutionId, Path.Combine(folder, "Panel.razor"), 1, 2000, CancellationToken.None);

        var tag = Assert.Single(view.Symbols, symbol => symbol.Kind == "ComponentTag");
        Assert.Equal("Badge", tag.Name);
        Assert.Equal(7, tag.Line);
        Assert.NotEmpty(tag.Id);

        Assert.Contains(view.Directives, directive => directive.Text.StartsWith("@page", StringComparison.Ordinal));
        Assert.Contains(view.Directives, directive => directive.Text.StartsWith("@inject", StringComparison.Ordinal));
    }

    /// <summary>The two ways this file can be read differ in what the answer may be trusted about, and they look
    /// identical unless the answer says which one it was.</summary>
    [Fact]
    public async Task ReadAsync_MarkupTheGrammarReads_SaysSoRatherThanLeavingTheWayItWasReadUnstated()
    {
        var view = await fixture.Reader.ReadAsync(
            solutionId, Path.Combine(folder, "Panel.razor"), 1, 2000, CancellationToken.None);

        Assert.Contains(view.Notes, note => note.Contains("read by the Razor grammar", StringComparison.Ordinal));
        Assert.DoesNotContain(view.Notes, note => note.Contains("scanning the text", StringComparison.Ordinal));
    }

    [Fact]
    public async Task FindAsync_ParameterIdFromOutline_ReturnsAuthoredMarkupPositions()
    {
        var view = await fixture.Reader.ReadAsync(
            solutionId, Path.Combine(folder, "Badge.razor"), 1, 2000, CancellationToken.None);

        var label = Assert.Single(view.Symbols, symbol => symbol.Name == "Label");

        var references = await fixture.References.FindAsync(
            solutionId, new SymbolQuery(label.Id, null, null, 0, 0), 10, CancellationToken.None);

        Assert.Contains(references.Items, reference =>
            reference.File.EndsWith("Panel.razor", StringComparison.Ordinal) && reference.Line > 0);
    }

    [Fact]
    public async Task UnusedAsync_ComponentMembers_ReportsOnlyUnusedOnesAtAuthoredLocations()
    {
        var page = await fixture.Auditor.UnusedAsync(solutionId, null, 300, CancellationToken.None);

        var ghost = Assert.Single(page.Items, finding => finding.Symbol.Name == "Ghost");
        Assert.True(ghost.Symbol.File?.EndsWith("Badge.razor", StringComparison.Ordinal), "The finding must point at the authored file.");
        Assert.Equal(11, ghost.Symbol.Line);
        Assert.False(ghost.Symbol.IsGenerated);

        var services = Assert.Single(page.Items, finding => finding.Symbol.Name == "Services");
        Assert.True(services.Symbol.File?.EndsWith("Panel.razor", StringComparison.Ordinal));

        Assert.DoesNotContain(page.Items, finding => finding.Symbol.Name is "Label" or "Count" or "Navigation");
        Assert.DoesNotContain(page.Items, finding => finding.Symbol.Kind == "NamedType");
    }

    [Fact]
    public async Task SearchAsync_ComponentAndItsMembers_PointAtTheMarkupFile()
    {
        var members = await fixture.Symbols.SearchAsync(
            solutionId, "Label", SymbolKindFilter.Any, 30, CancellationToken.None);

        var label = Assert.Single(members, match => match.Name == "Label" && match.Container == "Blazing.Badge");
        Assert.True(label.File?.EndsWith("Badge.razor", StringComparison.Ordinal), "A @code member must point at its authored file.");
        Assert.Equal(5, label.Line);

        var types = await fixture.Symbols.SearchAsync(
            solutionId, "Badge", SymbolKindFilter.Type, 30, CancellationToken.None);

        var badge = Assert.Single(types, match => match.Name == "Badge");
        Assert.True(badge.File?.EndsWith("Badge.razor", StringComparison.Ordinal), "A component must point at its markup file, not into obj/.");
        Assert.Equal(0, badge.Line);
        Assert.True(badge.IsGenerated);
    }

    [Fact]
    public async Task PlanAsync_MultilineTagAndCommentedTag_EditsOnlyTheRealTag()
    {
        var view = await fixture.Reader.ReadAsync(
            solutionId, Path.Combine(folder, "Wide.razor"), 1, 2000, CancellationToken.None);

        var tag = Assert.Single(view.Symbols, symbol => symbol.Kind == "ComponentTag");
        Assert.Equal("Badge", tag.Name);
        Assert.Equal(3, tag.Line);

        var plan = await fixture.Renames.PlanAsync(
            solutionId, new SymbolQuery(tag.Id, null, null, 0, 0), "Chip", new RenameScope(), CancellationToken.None);

        var wide = Assert.Single(plan.Edits, edit => edit.File.EndsWith("Wide.razor", StringComparison.Ordinal));
        var opening = Assert.Single(wide.Lines);
        Assert.Equal(3, opening.Line);
        Assert.Contains("<Chip", opening.After, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReadAsync_MarkupWithRenderCosts_ReportsSnrIssuesAtTheirLines()
    {
        var view = await fixture.Reader.ReadAsync(
            solutionId, Path.Combine(folder, "Board.razor"), 1, 2000, CancellationToken.None);

        // Asserted as one string so a failure names every issue that did come back. This test has failed
        // intermittently and Assert.Single reports only that a count was wrong, not what was found instead.
        var costs = string.Join(
            ", ",
            view.Issues.Items
                .Where(issue => issue.Id.StartsWith("SNR", StringComparison.Ordinal))
                .Select(issue => $"{issue.Id}@{issue.Line}"));

        Assert.Equal("SNR001@1, SNR002@3, SNR003@11", costs);
        Assert.Contains(view.Notes, note => note.Contains("render-cost", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ReadAsync_LambdaOnComponentInsideKeyedLoop_ReportsSnr002AtTheTag()
    {
        var view = await fixture.Reader.ReadAsync(
            solutionId, Path.Combine(folder, "Rows.razor"), 1, 2000, CancellationToken.None);

        var lambda = Assert.Single(view.Issues.Items, issue => issue.Id == "SNR002");

        Assert.Equal(3, lambda.Line);
        Assert.DoesNotContain(view.Issues.Items, issue => issue.Id == "SNR001");
    }

    [Fact]
    public async Task PlanAsync_Component_RewritesMarkupTagsAndRenamesItsFile()
    {
        var view = await fixture.Reader.ReadAsync(
            solutionId, Path.Combine(folder, "Badge.razor"), 1, 2000, CancellationToken.None);

        var component = Assert.Single(view.Symbols, symbol => symbol.Name == "Badge" && symbol.Kind == "NamedType");

        var plan = await fixture.Renames.PlanAsync(
            solutionId, new SymbolQuery(component.Id, null, null, 0, 0), "Chip", new RenameScope(), CancellationToken.None);

        var panel = Assert.Single(plan.Edits, edit => edit.File.EndsWith("Panel.razor", StringComparison.Ordinal));
        var line = Assert.Single(panel.Lines);
        Assert.Equal(7, line.Line);
        Assert.Contains("<Chip", line.After, StringComparison.Ordinal);
        Assert.DoesNotContain("<Badge", line.After, StringComparison.Ordinal);

        var file = Assert.Single(plan.Edits, edit => edit.RenamedTo is not null);
        Assert.EndsWith("Badge.razor", file.File, StringComparison.Ordinal);
        Assert.EndsWith("Chip.razor", file.RenamedTo!, StringComparison.Ordinal);

        Assert.DoesNotContain(plan.Unhandled, match =>
            match.File.EndsWith("Panel.razor", StringComparison.Ordinal) && match.Line == 7);
        Assert.Contains(plan.Notes, note => note.Contains("markup", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task CheckAsync_RepeatedMarkupBlock_IsReportedAsExtractionCandidate()
    {
        var report = await fixture.Surveyor.CheckAsync(solutionId, 50, CancellationToken.None);

        var finding = Assert.Single(report.Findings, candidate => candidate.Rule == "markup-duplication");

        Assert.True(finding.File?.EndsWith("Cards.razor", StringComparison.Ordinal), "The finding must point at the file holding the first occurrence.");
        Assert.Equal(1, finding.Line);
        Assert.Contains("repeats 2 times", finding.Message, StringComparison.Ordinal);
        Assert.Contains("Cards.razor:6", finding.Message, StringComparison.Ordinal);
        Assert.Contains(report.Notes, note => note.Contains("markup-duplication", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PlanAsync_ComponentParameter_EditsAttributeAssignmentsInConsumerMarkup()
    {
        var view = await fixture.Reader.ReadAsync(
            solutionId, Path.Combine(folder, "Badge.razor"), 1, 2000, CancellationToken.None);

        var label = Assert.Single(view.Symbols, symbol => symbol.Name == "Label");

        var plan = await fixture.Renames.PlanAsync(
            solutionId, new SymbolQuery(label.Id, null, null, 0, 0), "Caption", new RenameScope(), CancellationToken.None);

        var badge = Assert.Single(plan.Edits, edit => edit.File.EndsWith("Badge.razor", StringComparison.Ordinal));
        var declaration = Assert.Single(badge.Lines, line => line.Line == 5);
        Assert.Contains("public string? Caption", declaration.After, StringComparison.Ordinal);
        var use = Assert.Single(badge.Lines, line => line.Line == 1);
        Assert.Contains("@Caption", use.After, StringComparison.Ordinal);

        var panel = Assert.Single(plan.Edits, edit => edit.File.EndsWith("Panel.razor", StringComparison.Ordinal));
        var attribute = Assert.Single(panel.Lines);
        Assert.Equal(7, attribute.Line);
        Assert.Contains("Caption=\"item\"", attribute.After, StringComparison.Ordinal);
        Assert.DoesNotContain("Label=", attribute.After, StringComparison.Ordinal);

        Assert.Contains(plan.Edits, edit => edit.File.EndsWith("Board.razor", StringComparison.Ordinal));
        Assert.Contains(plan.Edits, edit => edit.File.EndsWith("Cards.razor", StringComparison.Ordinal));
        Assert.Contains(plan.Notes, note => note.Contains("@code", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExtractAsync_BlockWithUnresolvedNames_NamesLeaksThenPlansCleanlyOnceMapped()
    {
        var cards = Path.Combine(folder, "Cards.razor");

        var probe = await fixture.Extractor.ExtractAsync(
            solutionId, new ExtractionRequest(cards, 1, 4, "TCard", null, null, null, null), CancellationToken.None);

        Assert.Contains("LeftTitle", probe.Unresolved);
        Assert.DoesNotContain("Badge", probe.Unresolved);
        Assert.Empty(probe.Edits);
        Assert.Null(probe.Replacement);
        Assert.Contains(probe.Notes, note => note.Contains("substitutions", StringComparison.Ordinal));

        var plan = await fixture.Extractor.ExtractAsync(
            solutionId,
            new ExtractionRequest(
                cards,
                1,
                4,
                "TCard",
                new Dictionary<string, string> { ["LeftTitle"] = "Title" },
                "@code {\n    [Parameter]\n    public string? Title { get; set; }\n}",
                "<TCard Title=\"@LeftTitle\" />",
                null),
            CancellationToken.None);

        Assert.Empty(plan.Unresolved);

        var component = Assert.Single(plan.Edits);
        Assert.EndsWith("TCard.razor", component.File, StringComparison.Ordinal);
        Assert.Contains("<h2>@Title</h2>", component.Content, StringComparison.Ordinal);
        Assert.Contains("[Parameter]", component.Content, StringComparison.Ordinal);

        Assert.NotNull(plan.Replacement);
        Assert.EndsWith("Cards.razor", plan.Replacement!.File, StringComparison.Ordinal);
        Assert.Equal(1, plan.Replacement.StartLine);
        Assert.Equal(4, plan.Replacement.EndLine);
        Assert.Equal("<TCard Title=\"@LeftTitle\" />", plan.Replacement.Text);
    }

    [Fact]
    public async Task ExtractAsync_RangeCuttingAnElementApart_IsRefusedWithoutEdits()
    {
        var plan = await fixture.Extractor.ExtractAsync(
            solutionId,
            new ExtractionRequest(Path.Combine(folder, "Cards.razor"), 2, 4, "TCard", null, null, null, null),
            CancellationToken.None);

        Assert.Empty(plan.Edits);
        Assert.Contains(plan.Notes, note => note.Contains("<div>", StringComparison.Ordinal)
            && note.Contains("cut it apart", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExtractAsync_BlockFullyInsideContainer_IsNotRefusedAsBoundaryCut()
    {
        var probe = await fixture.Extractor.ExtractAsync(
            solutionId,
            new ExtractionRequest(Path.Combine(folder, "Cards.razor"), 2, 3, "TCardBody", null, null, null, null),
            CancellationToken.None);

        Assert.DoesNotContain(probe.Notes, note => note.Contains("cut it apart", StringComparison.Ordinal));
        Assert.Contains("LeftTitle", probe.Unresolved);
    }

    [Fact]
    public async Task ReadAsync_MarkupWithAPostSixDirective_StillReadsElementsFromTheParser()
    {
        var view = await fixture.Reader.ReadAsync(
            solutionId, Path.Combine(folder, "Modern.razor"), 1, 2000, CancellationToken.None);

        var tag = Assert.Single(view.Symbols, symbol => symbol.Kind == "ComponentTag");

        // Line 5, not 3: the tag inside the Razor comment is excluded, which only the element tree does.
        // @rendermode is unknown to the 6.0 grammar but reads as an expression, so the tree survives it.
        Assert.Equal(5, tag.Line);
    }

    /// <summary>A closing tag is where the element's own span ends. Everything else that reads like one — inside
    /// an attribute value, inside a comment — is text, and a rename that rewrites it corrupts the markup.</summary>
    [Fact]
    public async Task PlanAsync_ClosingTagInsideAnAttributeValue_IsLeftAlone()
    {
        var view = await fixture.Reader.ReadAsync(
            solutionId, Path.Combine(folder, "Badge.razor"), 1, 2000, CancellationToken.None);

        var component = Assert.Single(view.Symbols, symbol => symbol.Name == "Badge" && symbol.Kind == "NamedType");

        var plan = await fixture.Renames.PlanAsync(
            solutionId,
            new SymbolQuery(component.Id, null, null, 0, 0),
            "Chip",
            new RenameScope(true, true, false, false),
            CancellationToken.None);

        var quoted = Assert.Single(plan.Edits, edit => edit.File.EndsWith("Quoted.razor", StringComparison.Ordinal));

        var attribute = Assert.Single(quoted.Lines, line => line.Line == 1);
        Assert.Contains("<Chip Label=", attribute.After, StringComparison.Ordinal);
        Assert.Contains("</Badge> to close it", attribute.After, StringComparison.Ordinal);

        Assert.Single(quoted.Lines, line => line.Line == 5 && line.After.Contains("</Chip>", StringComparison.Ordinal));
    }

    /// <summary>Where a loop ends is a question about the block, not about the braces on the lines: a brace in
    /// markup text is text, and counting it swallowed everything below the loop.</summary>
    [Fact]
    public async Task ReadAsync_BraceInMarkupTextInsideALoop_KeepsTheLoopFromSwallowingWhatFollowsIt()
    {
        var view = await fixture.Reader.ReadAsync(
            solutionId, Path.Combine(folder, "Braces.razor"), 1, 2000, CancellationToken.None);

        var costs = string.Join(
            ", ",
            view.Issues.Items
                .Where(issue => issue.Id.StartsWith("SNR", StringComparison.Ordinal))
                .Select(issue => $"{issue.Id}@{issue.Line}"));

        // The lambda on line 8 is outside the loop, so it costs nothing per item and is not a finding.
        Assert.Equal("SNR001@1", costs);
    }

    [Fact]
    public async Task ReadAsync_ComponentInNestedFolder_MapsToItsOwnGeneratedCode()
    {
        var view = await fixture.Reader.ReadAsync(
            solutionId, Path.Combine(folder, "Components", "Pages", "Tile.razor"), 1, 2000, CancellationToken.None);

        var component = Assert.Single(view.Symbols, symbol => symbol.Kind == "NamedType");

        Assert.Equal("Blazing.Components.Pages", component.Container);
        Assert.Single(view.Symbols, symbol => symbol.Name == "NestedOnly");
    }

    [Fact]
    public async Task ReadAsync_ComponentSharingItsLeafNameWithANestedOne_MapsToItsOwnGeneratedCode()
    {
        var view = await fixture.Reader.ReadAsync(
            solutionId, Path.Combine(folder, "Tile.razor"), 1, 2000, CancellationToken.None);

        var component = Assert.Single(view.Symbols, symbol => symbol.Kind == "NamedType");

        Assert.Equal("Blazing", component.Container);
        Assert.Single(view.Symbols, symbol => symbol.Name == "RootOnly");
        Assert.DoesNotContain(view.Symbols, symbol => symbol.Name == "NestedOnly");
    }

    [Fact]
    public async Task ReadAsync_MarkupEditedAfterLoading_IsReadFreshFromDisk()
    {
        var badge = Path.Combine(folder, "Badge.razor");
        var edited = Badge.Replace("public int Doubled()", "public int Tripled()", StringComparison.Ordinal);

        await File.WriteAllTextAsync(badge, edited);
        File.SetLastWriteTimeUtc(badge, DateTime.UtcNow.AddSeconds(2));

        try
        {
            var view = await fixture.Reader.ReadAsync(solutionId, badge, 1, 2000, CancellationToken.None);

            Assert.Single(view.Symbols, symbol => symbol.Name == "Tripled");
            Assert.DoesNotContain(view.Symbols, symbol => symbol.Name == "Doubled");
        }
        finally
        {
            await File.WriteAllTextAsync(badge, Badge);
        }
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose()
    {
        fixture.Dispose();
        Directory.Delete(folder, recursive: true);
    }

    private async Task RestoreAsync()
    {
        var restore = Process.Start(new ProcessStartInfo("dotnet", "restore")
        {
            WorkingDirectory = folder,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        }) ?? throw new InvalidOperationException("dotnet restore did not start.");

        await restore.WaitForExitAsync(CancellationToken.None);

        if (restore.ExitCode != 0)
            throw new InvalidOperationException($"dotnet restore failed: {await restore.StandardError.ReadToEndAsync()}");
    }
}
