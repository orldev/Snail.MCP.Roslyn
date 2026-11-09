using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Snail.MCP.Roslyn.Tools.Prompts;

namespace Snail.MCP.Roslyn.Tools;

[McpServerToolType]
internal sealed class SolutionTools(ISolutionHost host, IDiagnosticReader diagnostics, ISolutionSurveyor surveyor, IFileReader reader)
{
    [McpServerTool(Name = "load_solution", ReadOnly = false, Destructive = false, OpenWorld = true)]
    [Description(ToolDescriptions.LoadSolution)]
    public Task<CallToolResult> LoadSolution(
        [Description("Absolute path to a .sln, .slnx or .csproj file, or a directory containing one.")]
        string path,
        CancellationToken cancellationToken,
        IProgress<ProgressNotificationValue> progress,
        [Description("Also count compiler errors and warnings. Off by default: it costs a full build and the numbers go stale on the next edit.")]
        bool countDiagnostics = false) =>
        host.LoadAsync(path, cancellationToken, countDiagnostics, new ToolProgress(progress))
            .AnsweredAsync(RoslynJson.Default.WorkspaceReport);

    [McpServerTool(Name = "get_diagnostics", ReadOnly = true, OpenWorld = false)]
    [Description(ToolDescriptions.GetDiagnostics)]
    public Task<CallToolResult> GetDiagnostics(
        [Description(ParameterDescriptions.Solution)]
        string solutionId,
        CancellationToken cancellationToken,
        [Description("Optional absolute file or directory path that narrows the results.")]
        string? scope = null,
        [Description("Lowest severity to include: Info, Warning or Error.")]
        IssueSeverity minimum = IssueSeverity.Warning,
        [Description("Maximum issues to return.")]
        int limit = 50,
        [Description("Include diagnostics that only exist in generated code the caller cannot edit.")]
        bool includeGenerated = false,
        [Description("Also run the analyzers the project's own build runs, at the severity it gives them. Costs more than the compiler alone: narrow scope to the file you changed.")]
        bool analyzers = false) =>
        diagnostics.ReadAsync(solutionId, scope, minimum, limit, includeGenerated, analyzers, cancellationToken)
            .AnsweredAsync(RoslynJson.Default.IssuePage);

    [McpServerTool(Name = "read_file", ReadOnly = true, OpenWorld = false)]
    [Description(ToolDescriptions.ReadFile)]
    public Task<CallToolResult> ReadFile(
        [Description(ParameterDescriptions.Solution)]
        string solutionId,
        [Description("Absolute path of the file to read.")]
        string file,
        CancellationToken cancellationToken,
        [Description("One-based line to start from.")]
        int startLine = 1,
        [Description("Maximum lines of text to return.")]
        int maxLines = 2000) =>
        reader.ReadAsync(solutionId, file, startLine, maxLines, cancellationToken)
            .AnsweredAsync(RoslynJson.Default.FileView);

    [McpServerTool(Name = "get_solution_overview", ReadOnly = true, OpenWorld = false)]
    [Description(ToolDescriptions.GetSolutionOverview)]
    public Task<CallToolResult> GetSolutionOverview(
        [Description(ParameterDescriptions.Solution)]
        string solutionId,
        CancellationToken cancellationToken,
        IProgress<ProgressNotificationValue> progress,
        [Description("Maximum entries per section of the answer.")]
        int limit = 20) =>
        surveyor.OverviewAsync(solutionId, limit, cancellationToken, new ToolProgress(progress))
            .AnsweredAsync(RoslynJson.Default.SolutionOverview);

    [McpServerTool(Name = "check_architecture", ReadOnly = true, OpenWorld = false)]
    [Description(ToolDescriptions.CheckArchitecture)]
    public Task<CallToolResult> CheckArchitecture(
        [Description(ParameterDescriptions.Solution)]
        string solutionId,
        CancellationToken cancellationToken,
        [Description("Maximum findings to return.")]
        int limit = 50) =>
        surveyor.CheckAsync(solutionId, limit, cancellationToken)
            .AnsweredAsync(RoslynJson.Default.ArchitectureReport);

    [McpServerTool(Name = "health_check", ReadOnly = true, OpenWorld = false)]
    [Description(ToolDescriptions.HealthCheck)]
    public Task<CallToolResult> HealthCheck(
        [Description(ParameterDescriptions.Solution)]
        string solutionId,
        CancellationToken cancellationToken) =>
        surveyor.HealthAsync(solutionId, cancellationToken)
            .AnsweredAsync(RoslynJson.Default.HealthReport);

    [McpServerTool(Name = "unload_solution", ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description(ToolDescriptions.UnloadSolution)]
    public Task<CallToolResult> UnloadSolution(
        [Description(ParameterDescriptions.Solution)]
        string solutionId,
        CancellationToken cancellationToken) =>
        host.UnloadAsync(solutionId, cancellationToken)
            .AnsweredAsync(RoslynJson.Default.UnloadReport);
}
