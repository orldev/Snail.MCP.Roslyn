using System.ComponentModel;
using ModelContextProtocol.Server;
using Snail.MCP.Roslyn.Tools.Prompts;

namespace Snail.MCP.Roslyn.Tools;

[McpServerToolType]
public sealed class AnalysisTools(IImpactAnalyzer impact, IAnalyzerRunner analyzers, ICodeValidator validator)
{
    [McpServerTool(Name = "analyze_change_impact")]
    [Description(ToolDescriptions.AnalyzeChangeImpact)]
    public Task<ImpactReport> AnalyzeChangeImpact(
        [Description("Solution id returned by load_solution.")]
        string solutionId,
        CancellationToken cancellationToken,
        [Description("Symbol id from search_symbols. Give this, a bare name, or a file position.")]
        string? symbolId = null,
        [Description("Bare symbol name; used only when it matches exactly one symbol.")]
        string? name = null,
        [Description("Absolute file path, when addressing by position.")]
        string? file = null,
        [Description("One-based line, when addressing by position.")]
        int line = 0,
        [Description("One-based column, when addressing by position.")]
        int column = 0,
        [Description("Maximum entries per section of the answer.")]
        int limit = 50) =>
        impact.AnalyzeAsync(solutionId, new SymbolQuery(symbolId, name, file, line, column), limit, cancellationToken);

    [McpServerTool(Name = "run_analyzers")]
    [Description(ToolDescriptions.RunAnalyzers)]
    public Task<AnalyzerPage> RunAnalyzers(
        [Description("Solution id returned by load_solution.")]
        string solutionId,
        CancellationToken cancellationToken,
        [Description("Optional absolute file or directory path that narrows the results.")]
        string? scope = null,
        [Description("Analyzer category to keep, such as Performance, Design, Reliability or Usage.")]
        string? category = null,
        [Description("Lowest severity to include: Info, Warning or Error.")]
        IssueSeverity minimum = IssueSeverity.Info,
        [Description("Maximum findings to return.")]
        int limit = 50) =>
        analyzers.RunAsync(solutionId, scope, category, minimum, limit, cancellationToken);

    [McpServerTool(Name = "get_code_fixes")]
    [Description(ToolDescriptions.GetCodeFixes)]
    public Task<CodeFixPage> GetCodeFixes(
        [Description("Solution id returned by load_solution.")]
        string solutionId,
        [Description("Absolute path of the file the diagnostic points at.")]
        string file,
        [Description("One-based line the diagnostic points at.")]
        int line,
        CancellationToken cancellationToken,
        [Description("Diagnostic id to fix, such as CS0103; the first diagnostic on the line when omitted.")]
        string? diagnosticId = null) =>
        analyzers.FixesAsync(solutionId, file, line, diagnosticId, cancellationToken);

    [McpServerTool(Name = "validate")]
    [Description(ToolDescriptions.Validate)]
    public Task<ValidationResult> Validate(
        [Description("Solution id returned by load_solution.")]
        string solutionId,
        [Description("Absolute path the contents are meant for; the file need not exist yet.")]
        string file,
        [Description("The complete proposed contents of that file.")]
        string content,
        CancellationToken cancellationToken) =>
        validator.ValidateAsync(solutionId, file, content, cancellationToken);
}
