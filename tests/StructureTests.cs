namespace Snail.MCP.Roslyn.Tests;

/// <summary>Structural refactorings return edits, state their limits, and never write.</summary>
[Collection(nameof(AnalysisCollection))]
public sealed class StructureTests(AnalysisFixture fixture)
{
    [Fact]
    public async Task Moving_a_type_leaves_the_files_on_disk_untouched()
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
    public async Task A_namespace_rename_of_a_different_depth_proposes_no_edits_and_says_why()
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
    public async Task A_namespace_rename_rewrites_the_declaration_and_the_files_that_use_it()
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
    public async Task Changing_a_signature_edits_the_declaration_and_lists_every_call_site_as_a_conflict()
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
