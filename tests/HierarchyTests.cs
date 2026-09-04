namespace Snail.MCP.Roslyn.Tests;

[Collection(nameof(AnalysisCollection))]
public sealed class HierarchyTests(AnalysisFixture fixture)
{
    [Fact]
    public async Task ImplementationsAsync_Interface_ReturnsImplementingAdapter()
    {
        var found = await fixture.Hierarchy.ImplementationsAsync(
            fixture.SolutionId,
            new SymbolQuery(null, nameof(IPathMap), null, 0, 0),
            50,
            CancellationToken.None);

        Assert.Contains(found, match => match.Name == "RootPathMap");
    }

    [Fact]
    public async Task HierarchyAsync_Type_ReturnsInterfacesItImplements()
    {
        var hierarchy = await fixture.Hierarchy.HierarchyAsync(
            fixture.SolutionId,
            new SymbolQuery(null, "RootPathMap", null, 0, 0),
            CancellationToken.None);

        Assert.NotNull(hierarchy);
        Assert.Contains(hierarchy.Interfaces, match => match.Name == nameof(IPathMap));
    }

    [Fact]
    public async Task CallersAsync_InterfaceMethod_ReturnsCallingMethods()
    {
        var callers = await fixture.Hierarchy.CallersAsync(
            fixture.SolutionId,
            await fixture.MemberAsync(nameof(IPathMap.ToCaller), nameof(IPathMap)),
            50,
            CancellationToken.None);

        Assert.NotEmpty(callers);
    }
}
