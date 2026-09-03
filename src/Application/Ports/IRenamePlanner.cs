namespace Snail.MCP.Roslyn.Application.Ports;

/// <summary>Works out a rename with the compiler and hands it back as edits; never writes to disk.</summary>
public interface IRenamePlanner
{
    Task<RenamePlan> PlanAsync(
        string solutionId,
        SymbolQuery query,
        string newName,
        RenameScope scope,
        CancellationToken cancellationToken);
}
