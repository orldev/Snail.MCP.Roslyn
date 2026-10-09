using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Snail.MCP.Roslyn.Tools.Prompts;

namespace Snail.MCP.Roslyn.Tools;

[McpServerToolType]
public sealed class AnalysisTools(IImpactAnalyzer impact, IAnalyzerRunner analyzers, ICodeValidator validator)
{
    [McpServerTool(Name = "analyze_change_impact", ReadOnly = true, OpenWorld = false)]
    [Description(ToolDescriptions.AnalyzeChangeImpact)]
    public Task<ImpactReport> AnalyzeChangeImpact(
        [Description(ParameterDescriptions.Solution)]
        string solutionId,
        CancellationToken cancellationToken,
        [Description(ParameterDescriptions.SymbolId)]
        string? symbolId = null,
        [Description(ParameterDescriptions.SymbolName)]
        string? name = null,
        [Description(ParameterDescriptions.File)]
        string? file = null,
        [Description(ParameterDescriptions.Line)]
        int line = 0,
        [Description(ParameterDescriptions.Column)]
        int column = 0,
        [Description("Maximum entries per section of the answer.")]
        int limit = 50) =>
        impact.AnalyzeAsync(solutionId, new SymbolQuery(symbolId, name, file, line, column), limit, cancellationToken);

    [McpServerTool(Name = "run_analyzers", ReadOnly = true, OpenWorld = false)]
    [Description(ToolDescriptions.RunAnalyzers)]
    public Task<AnalyzerPage> RunAnalyzers(
        [Description(ParameterDescriptions.Solution)]
        string solutionId,
        CancellationToken cancellationToken,
        IProgress<ProgressNotificationValue> progress,
        [Description("Optional absolute file or directory path that narrows the results.")]
        string? scope = null,
        [Description("Analyzer category to keep, such as Performance, Design, Reliability or Usage.")]
        string? category = null,
        [Description("Lowest severity to include: Info, Warning or Error.")]
        IssueSeverity minimum = IssueSeverity.Info,
        [Description("Maximum findings to return.")]
        int limit = 50) =>
        analyzers.RunAsync(solutionId, scope, category, minimum, limit, cancellationToken, new ToolProgress(progress));

    [McpServerTool(Name = "get_code_fixes", ReadOnly = true, OpenWorld = false)]
    [Description(ToolDescriptions.GetCodeFixes)]
    public Task<CodeFixPage> GetCodeFixes(
        [Description(ParameterDescriptions.Solution)]
        string solutionId,
        [Description("Absolute path of the file the diagnostic points at.")]
        string file,
        [Description("One-based line the diagnostic points at.")]
        int line,
        CancellationToken cancellationToken,
        [Description("Diagnostic id to fix, such as CS0103; the first diagnostic on the line when omitted.")]
        string? diagnosticId = null) =>
        analyzers.FixesAsync(solutionId, file, line, diagnosticId, cancellationToken);

    [McpServerTool(Name = "validate", ReadOnly = true, OpenWorld = false)]
    [Description(ToolDescriptions.Validate)]
    public Task<ValidationResult> Validate(
        [Description(ParameterDescriptions.Solution)]
        string solutionId,
        [Description("Absolute path the contents are meant for; the file need not exist yet.")]
        string file,
        [Description("The complete proposed contents of that file.")]
        string content,
        CancellationToken cancellationToken) =>
        validator.ValidateAsync(solutionId, file, content, cancellationToken);
}
