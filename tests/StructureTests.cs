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

    [Fact]
    public async Task ChangeSignatureAsync_InterfaceMethod_EditsDeclarationAndListsCallSitesAsConflicts()
    {
        var plan = await fixture.Structure.ChangeSignatureAsync(
            fixture.SolutionId,
            await fixture.MemberAsync(nameof(IPathMap.ToCaller), nameof(IPathMap)),
            new SignatureChange(["string processPath", "bool strict"], null),
            CancellationToken.None);

        Assert.NotEmpty(plan.Edits);
        Assert.NotEmpty(plan.Conflicts);
        Assert.Contains(plan.Notes, note => note.Contains("work list", StringComparison.Ordinal));
    }

    [Fact]
    public async Task OrganizeUsingsAsync_AnyFile_LeavesEveryLineBelowTheUsingBlockUntouched()
    {
        var plan = await fixture.Structure.OrganizeUsingsAsync(
            fixture.SolutionId, RepositoryPaths.Source, CancellationToken.None);

        Assert.NotEmpty(plan.Edits);
        Assert.All(plan.Edits, edit =>
        {
            var before = File.ReadAllLines(Path.Combine(RepositoryPaths.Root, edit.File));
            var after = edit.Content!.Split('\n').Select(line => line.TrimEnd('\r')).ToArray();

            var tail = before.SkipWhile(line => !line.StartsWith("namespace", StringComparison.Ordinal));
            var rewritten = after.SkipWhile(line => !line.StartsWith("namespace", StringComparison.Ordinal));

            Assert.Equal(tail, rewritten.Take(tail.Count()));
        });
    }

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

        var edit = Assert.Single(plan.Edits);

        Assert.Contains(expected, edit.Content);
    }

    [Fact]
    public async Task ChangeSignatureAsync_ParametersThatDoNotParse_ProposeNoEditsAndSayWhy()
    {
        var plan = await fixture.Structure.ChangeSignatureAsync(
            fixture.SolutionId,
            await fixture.MemberAsync(nameof(IPathMap.ToCaller), nameof(IPathMap)),
            new SignatureChange(["int ("], null),
            CancellationToken.None);

        Assert.Empty(plan.Edits);
        Assert.Contains(plan.Notes, note => note.Contains("does not parse", StringComparison.Ordinal));
    }
}
