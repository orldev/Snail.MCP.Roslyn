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
}
