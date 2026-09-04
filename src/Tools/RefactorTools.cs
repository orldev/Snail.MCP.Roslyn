using System.ComponentModel;
using ModelContextProtocol.Server;
using Snail.MCP.Roslyn.Tools.Prompts;

namespace Snail.MCP.Roslyn.Tools;

[McpServerToolType]
public sealed class RefactorTools(IRenamePlanner planner, IStructureRefactorer structure, IComponentExtractor extractor)
{
    [McpServerTool(Name = "extract_component")]
    [Description(ToolDescriptions.ExtractComponent)]
    public Task<ExtractionPlan> ExtractComponent(
        [Description(ParameterDescriptions.Solution)]
        string solutionId,
        [Description("Absolute path of the .razor file to extract from.")]
        string file,
        [Description("One-based first line of the block, inclusive.")]
        int startLine,
        [Description("One-based last line of the block, inclusive.")]
        int endLine,
        [Description("Name of the new component; also names the new file.")]
        string name,
        CancellationToken cancellationToken,
        [Description("Renames applied to the block, page-private name to parameter name; longest keys first. Omit on the first call to learn the leaks.")]
        Dictionary<string, string>? substitutions = null,
        [Description("The @code block of the new component, verbatim, declaring its [Parameter]s.")]
        string? code = null,
        [Description("Markup that replaces the block in the source file; a self-closing tag of the new component when omitted.")]
        string? tag = null,
        [Description("Absolute path for the new file; a sibling named after the component when omitted.")]
        string? newFile = null) =>
        extractor.ExtractAsync(
            solutionId,
            new ExtractionRequest(file, startLine, endLine, name, substitutions, code, tag, newFile),
            cancellationToken);

    [McpServerTool(Name = "rename_symbol")]
    [Description(ToolDescriptions.RenameSymbol)]
    public Task<RenamePlan> RenameSymbol(
        [Description(ParameterDescriptions.Solution)]
        string solutionId,
        [Description("New name for the symbol.")]
        string newName,
        CancellationToken cancellationToken,
        [Description(ParameterDescriptions.SymbolId)]
        string? symbolId = null,
        [Description("Bare symbol name; used only when it matches exactly one symbol, otherwise candidates come back.")]
        string? name = null,
        [Description(ParameterDescriptions.File)]
        string? file = null,
        [Description(ParameterDescriptions.Line)]
        int line = 0,
        [Description(ParameterDescriptions.Column)]
        int column = 0,
        [Description("Rename every overload of the method, not just this one.")]
        bool renameOverloads = true,
        [Description("Rename the file when its name matches the type.")]
        bool renameFile = true,
        [Description("Also rewrite occurrences inside comments.")]
        bool searchComments = false,
        [Description("Also rewrite occurrences inside string literals.")]
        bool searchStrings = false) =>
        planner.PlanAsync(
            solutionId,
            new SymbolQuery(symbolId, name, file, line, column),
            newName,
            new RenameScope(renameOverloads, renameFile, searchComments, searchStrings),
            cancellationToken);

    [McpServerTool(Name = "rename_namespace")]
    [Description(ToolDescriptions.RenameNamespace)]
    public Task<EditPlan> RenameNamespace(
        [Description(ParameterDescriptions.Solution)]
        string solutionId,
        [Description("Namespace as it is written today, fully qualified.")]
        string oldNamespace,
        [Description("Namespace it should read as, fully qualified and with the same number of segments.")]
        string newNamespace,
        CancellationToken cancellationToken,
        [Description("Also move the files so the folders match the new namespace.")]
        bool updateFolders = true) =>
        structure.RenameNamespaceAsync(solutionId, oldNamespace, newNamespace, updateFolders, cancellationToken);

    [McpServerTool(Name = "move_type")]
    [Description(ToolDescriptions.MoveType)]
    public Task<EditPlan> MoveType(
        [Description(ParameterDescriptions.Solution)]
        string solutionId,
        CancellationToken cancellationToken,
        [Description(ParameterDescriptions.SymbolId)]
        string? symbolId = null,
        [Description(ParameterDescriptions.TypeName)]
        string? name = null,
        [Description(ParameterDescriptions.File)]
        string? file = null,
        [Description(ParameterDescriptions.Line)]
        int line = 0,
        [Description(ParameterDescriptions.Column)]
        int column = 0,
        [Description("Absolute path of the file the type should live in; a sibling file named after the type when omitted.")]
        string? targetFile = null,
        [Description("Namespace the type should declare; unchanged when omitted.")]
        string? targetNamespace = null) =>
        structure.MoveTypeAsync(
            solutionId,
            new SymbolQuery(symbolId, name, file, line, column),
            targetFile,
            targetNamespace,
            cancellationToken);

    [McpServerTool(Name = "organize_usings")]
    [Description(ToolDescriptions.OrganizeUsings)]
    public Task<EditPlan> OrganizeUsings(
        [Description(ParameterDescriptions.Solution)]
        string solutionId,
        CancellationToken cancellationToken,
        [Description("Optional absolute file or directory path that narrows the work.")]
        string? scope = null) =>
        structure.OrganizeUsingsAsync(solutionId, scope, cancellationToken);

    [McpServerTool(Name = "change_signature")]
    [Description(ToolDescriptions.ChangeSignature)]
    public Task<EditPlan> ChangeSignature(
        [Description(ParameterDescriptions.Solution)]
        string solutionId,
        CancellationToken cancellationToken,
        [Description(ParameterDescriptions.SymbolId)]
        string? symbolId = null,
        [Description("Bare method name; used only when it matches exactly one method.")]
        string? name = null,
        [Description(ParameterDescriptions.File)]
        string? file = null,
        [Description(ParameterDescriptions.Line)]
        int line = 0,
        [Description(ParameterDescriptions.Column)]
        int column = 0,
        [Description("Parameters the method should take, each written as 'Type name'; unchanged when omitted.")]
        string[]? parameters = null,
        [Description("Return type the method should have; unchanged when omitted.")]
        string? returnType = null) =>
        structure.ChangeSignatureAsync(
            solutionId,
            new SymbolQuery(symbolId, name, file, line, column),
            new SignatureChange(parameters, returnType),
            cancellationToken);
}
