namespace Snail.MCP.Roslyn.Tests;

/// <summary>Inheritance and call edges come from the compiler, not from text.</summary>
[Collection(nameof(AnalysisCollection))]
public sealed class HierarchyTests(AnalysisFixture fixture)
{
    [Fact]
    public async Task An_interface_reports_the_adapter_that_implements_it()
    {
        var found = await fixture.Hierarchy.ImplementationsAsync(
            fixture.SolutionId,
            new SymbolQuery(null, nameof(IPathMap), null, 0, 0),
            50,
            CancellationToken.None);

        Assert.Contains(found, match => match.Name == "RootPathMap");
    }

    [Fact]
    public async Task A_type_reports_the_interfaces_it_carries()
    {
        var hierarchy = await fixture.Hierarchy.HierarchyAsync(
            fixture.SolutionId,
            new SymbolQuery(null, "RootPathMap", null, 0, 0),
            CancellationToken.None);

        Assert.NotNull(hierarchy);
        Assert.Contains(hierarchy.Interfaces, match => match.Name == nameof(IPathMap));
    }

    [Fact]
    public async Task A_method_reports_the_methods_that_call_it()
    {
        var callers = await fixture.Hierarchy.CallersAsync(
            fixture.SolutionId,
            await fixture.MemberAsync(nameof(IPathMap.ToCaller), nameof(IPathMap)),
            50,
            CancellationToken.None);

        Assert.NotEmpty(callers);
    }
}
