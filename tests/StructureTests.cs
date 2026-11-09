namespace Snail.MCP.Roslyn.Tests;

[Collection(nameof(AnalysisCollection))]
public sealed class StructureTests(AnalysisFixture fixture)
{
    [Fact]
    public async Task MoveTypeAsync_ValidTarget_ReturnsEditsAndLeavesDiskUntouched()
    {
        var origin = Path.Combine(RepositoryPaths.Source, "Domain", "LineEdit.cs");
        var before = await File.ReadAllTextAsync(origin);

        var plan = await fixture.Structure.MoveTypeAsync(
            fixture.SolutionId,
            new SymbolQuery(null, nameof(LineEdit), null, 0, 0),
            Path.Combine(RepositoryPaths.Source, "Domain", "Edits", "LineEdit.cs"),
            null,
            CancellationToken.None);

        Assert.NotEmpty(plan.Edits);
        Assert.Equal(before, await File.ReadAllTextAsync(origin));
        Assert.False(File.Exists(Path.Combine(RepositoryPaths.Source, "Domain", "Edits", "LineEdit.cs")));
    }

    [Fact]
    public async Task RenameNamespaceAsync_DifferentSegmentCount_ProposesNoEditsAndExplains()
    {
        var plan = await fixture.Structure.RenameNamespaceAsync(
            fixture.SolutionId,
            "Snail.MCP.Roslyn.Domain",
            "Snail.Domain",
            false,
            CancellationToken.None);

        Assert.Empty(plan.Edits);
        Assert.Contains(plan.Notes, note => note.Contains("different number of segments", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RenameNamespaceAsync_SameSegmentCount_EditsDeclarationAndUsers()
    {
        var plan = await fixture.Structure.RenameNamespaceAsync(
            fixture.SolutionId,
            "Snail.MCP.Roslyn.Domain",
            "Snail.MCP.Roslyn.Model",
            false,
            CancellationToken.None);

        Assert.NotEmpty(plan.Edits);
        Assert.Contains(plan.Edits, edit => edit.File.EndsWith("LineEdit.cs", StringComparison.Ordinal));
    }

    /// <summary>
    /// A changed middle segment is renamed where it stands. The plan renamed the last segment to the new middle one
    /// instead: Snail.MCP.Vault.Configuration to Snail.MCP.Obsidian.Configuration came out Snail.MCP.Vault.Obsidian
    /// (2026-10-17), as only a changed last segment was tested.
    /// </summary>
    [Fact]
    public async Task RenameNamespaceAsync_MiddleSegment_RenamesThatSegmentAndNamesWhatMovesWithIt()
    {
        var plan = await fixture.Structure.RenameNamespaceAsync(
            fixture.SolutionId,
            "Snail.MCP.Roslyn.Adapters.Workspaces",
            "Snail.MCP.Roslyn.Infrastructure.Workspaces",
            false,
            CancellationToken.None);

        var written = plan.Edits.SelectMany(edit => edit.Lines).Select(line => line.After).ToList();

        Assert.Contains(written, line => line.Contains("namespace Snail.MCP.Roslyn.Infrastructure.Workspaces;", StringComparison.Ordinal));
        Assert.DoesNotContain(written, line => line.Contains("Adapters.Infrastructure", StringComparison.Ordinal));
        Assert.Contains(plan.Notes, note => note.Contains("Snail.MCP.Roslyn.Adapters.Paths", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ChangeSignatureAsync_InterfaceMethod_EditsDeclarationAndListsCallSitesAsConflicts()
    {
        var plan = await fixture.Structure.ChangeSignatureAsync(
            fixture.SolutionId,
            await fixture.MemberAsync(nameof(IPathMap.ToCaller), nameof(IPathMap)),
            new SignatureChange(["string processPath", "bool strict"], null),
            CancellationToken.None);

        Assert.NotEmpty(plan.Replacements);
        Assert.NotEmpty(plan.Conflicts);
        Assert.Contains(plan.Notes, note => note.Contains("work list", StringComparison.Ordinal));
    }

    [Fact]
    public async Task OrganizeUsingsAsync_AnyFile_LeavesEveryLineBelowTheUsingBlockUntouched()
    {
        var plan = await fixture.Structure.OrganizeUsingsAsync(
            fixture.SolutionId, RepositoryPaths.Source, CancellationToken.None);

        Assert.NotEmpty(plan.Replacements);
        Assert.All(plan.Replacements, replacement =>
        {
            var before = File.ReadAllLines(Path.Combine(RepositoryPaths.Root, replacement.File));
            var after = Apply(before, replacement);

            var tail = before.SkipWhile(line => !line.StartsWith("namespace", StringComparison.Ordinal));
            var rewritten = after.SkipWhile(line => !line.StartsWith("namespace", StringComparison.Ordinal));

            Assert.Equal(tail, rewritten);
        });
    }

    /// <summary>A plan the caller cannot afford is a plan it never receives: the whole file came back once and
    /// the client rejected the answer on its token limit.</summary>
    [Fact]
    public async Task StructurePlans_AnyOperation_CarryNoFileTheCallerAlreadyHas()
    {
        var organize = await fixture.Structure.OrganizeUsingsAsync(
            fixture.SolutionId, RepositoryPaths.Source, CancellationToken.None);

        var signature = await fixture.Structure.ChangeSignatureAsync(
            fixture.SolutionId,
            await fixture.MemberAsync(nameof(IPathMap.ToCaller), nameof(IPathMap)),
            new SignatureChange(["string processPath", "bool strict"], null),
            CancellationToken.None);

        var move = await fixture.Structure.MoveTypeAsync(
            fixture.SolutionId,
            new SymbolQuery(null, nameof(LineEdit), null, 0, 0),
            Path.Combine(RepositoryPaths.Source, "Domain", "Edits", "LineEdit.cs"),
            null,
            CancellationToken.None);

        foreach (var plan in new[] { organize, signature, move })
        {
            Assert.All(plan.Edits, edit => Assert.True(
                edit.Content is null || !File.Exists(Path.Combine(RepositoryPaths.Root, edit.File)),
                $"{plan.Operation} returned the whole content of {edit.File}, which the caller can already read."));
        }
    }

    private static string[] Apply(string[] before, BlockReplacement replacement) =>
    [
        .. before.Take(replacement.StartLine - 1),
        .. replacement.Text.Split('\n').Select(line => line.TrimEnd('\r')),
        .. before.Skip(replacement.EndLine)
    ];

    [Theory]
    [InlineData("IDictionary<string, int> map", "IDictionary<string, int> map")]
    [InlineData("ref int counter", "ref int counter")]
    [InlineData("int retries = 5", "int retries = 5")]
    [InlineData("params string[] names", "params string[] names")]
    public async Task ChangeSignatureAsync_ParameterCarryingSpacesOrModifiers_IsWrittenBackVerbatim(
        string parameter,
        string expected)
    {
        var plan = await fixture.Structure.ChangeSignatureAsync(
            fixture.SolutionId,
            await fixture.MemberAsync(nameof(IPathMap.ToCaller), nameof(IPathMap)),
            new SignatureChange([parameter], null),
            CancellationToken.None);

        var replacement = Assert.Single(plan.Replacements);

        Assert.Contains(expected, replacement.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ChangeSignatureAsync_ParametersThatDoNotParse_ProposeNoEditsAndSayWhy()
    {
        var plan = await fixture.Structure.ChangeSignatureAsync(
            fixture.SolutionId,
            await fixture.MemberAsync(nameof(IPathMap.ToCaller), nameof(IPathMap)),
            new SignatureChange(["int ("], null),
            CancellationToken.None);

        Assert.Empty(plan.Replacements);
        Assert.Contains(plan.Notes, note => note.Contains("does not parse", StringComparison.Ordinal));
    }
}
