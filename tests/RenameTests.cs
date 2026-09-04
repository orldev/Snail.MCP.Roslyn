namespace Snail.MCP.Roslyn.Tests;

[Collection(nameof(AnalysisCollection))]
public sealed class RenameTests(AnalysisFixture fixture)
{
    [Fact]
    public async Task PlanAsync_Port_EditsImplementationAndRegistration()
    {
        var plan = await Plan(nameof(IRenamePlanner), "IRenameDesigner");

        Assert.Empty(plan.Conflicts);
        Assert.Contains(plan.Edits, edit => edit.File.EndsWith("RoslynRenamer.cs", StringComparison.Ordinal));
        Assert.Contains(plan.Edits, edit => edit.File.EndsWith("ServiceCollectionExtensions.cs", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PlanAsync_TypeNamedAfterItsFile_RenamesThatFile()
    {
        var plan = await Plan(RoslynRenamerName, "RenameDesigner");

        Assert.Contains(
            plan.Edits,
            edit => edit.RenamedTo is not null && edit.RenamedTo.EndsWith("RenameDesigner.cs", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PlanAsync_BareNameMatchingSeveralSymbols_ReturnsCandidatesAndNoEdits()
    {
        var plan = await fixture.Renames.PlanAsync(
            fixture.SolutionId,
            new SymbolQuery(null, "Dispose", null, 0, 0),
            "Release",
            new RenameScope(),
            CancellationToken.None);

        Assert.Empty(plan.Edits);
        Assert.True(plan.Candidates.Count > 1);
    }

    [Fact]
    public async Task PlanAsync_AnyRename_LeavesDiskUntouched()
    {
        var file = Path.Combine(RepositoryPaths.Source, "Adapters", "Workspaces", "RoslynRenamer.cs");
        var before = await File.ReadAllTextAsync(file, CancellationToken.None);

        await Plan(nameof(IRenamePlanner), "IRenameDesigner");

        Assert.Equal(before, await File.ReadAllTextAsync(file, CancellationToken.None));
    }

    private const string RoslynRenamerName = "RoslynRenamer";

    private async Task<RenamePlan> Plan(string name, string newName)
    {
        var matches = await fixture.Symbols.SearchAsync(
            fixture.SolutionId, name, SymbolKindFilter.Type, 5, CancellationToken.None);
        var match = matches.Single(candidate => candidate.Name == name);

        return await fixture.Renames.PlanAsync(
            fixture.SolutionId,
            new SymbolQuery(match.Id, null, null, 0, 0),
            newName,
            new RenameScope(),
            CancellationToken.None);
    }

    [Fact]
    public async Task PlanAsync_IndentedLine_KeepsIndentationInTheEdit()
    {
        var plan = await Plan(nameof(IRenamePlanner), "IRenameDesigner");

        var indented = plan.Edits
            .SelectMany(edit => edit.Lines)
            .Where(line => line.Before.StartsWith(' '))
            .ToList();

        Assert.NotEmpty(indented);
        Assert.All(indented, line => Assert.StartsWith(" ", line.After, StringComparison.Ordinal));
    }
}
