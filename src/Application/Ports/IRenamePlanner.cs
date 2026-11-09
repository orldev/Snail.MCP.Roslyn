namespace Snail.MCP.Roslyn.Application.Ports;

/// <summary>Works the rename out with the compiler and returns edits; never writes to disk.</summary>
internal interface IRenamePlanner
{
    Task<RenamePlan> PlanAsync(
        string solutionId,
        SymbolQuery query,
        string newName,
        RenameScope scope,
        CancellationToken cancellationToken);
}
