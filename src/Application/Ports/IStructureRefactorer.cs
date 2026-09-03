namespace Snail.MCP.Roslyn.Application.Ports;

/// <summary>Moves code between namespaces, files and shapes; returns edits and never writes to disk.</summary>
public interface IStructureRefactorer
{
    Task<EditPlan> RenameNamespaceAsync(
        string solutionId,
        string oldNamespace,
        string newNamespace,
        bool updateFolders,
        CancellationToken cancellationToken);

    Task<EditPlan> MoveTypeAsync(
        string solutionId,
        SymbolQuery query,
        string? targetFile,
        string? targetNamespace,
        CancellationToken cancellationToken);

    Task<EditPlan> OrganizeUsingsAsync(string solutionId, string? scope, CancellationToken cancellationToken);

    Task<EditPlan> ChangeSignatureAsync(
        string solutionId,
        SymbolQuery query,
        SignatureChange change,
        CancellationToken cancellationToken);
}
