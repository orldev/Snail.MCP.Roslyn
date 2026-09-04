using System.ComponentModel;
using ModelContextProtocol.Server;
using Snail.MCP.Roslyn.Tools.Prompts;

namespace Snail.MCP.Roslyn.Tools;

[McpServerToolType]
public sealed class SolutionTools(ISolutionHost host, IDiagnosticReader diagnostics, ISolutionSurveyor surveyor, IFileReader reader)
{
    [McpServerTool(Name = "load_solution")]
    [Description(ToolDescriptions.LoadSolution)]
    public Task<WorkspaceReport> LoadSolution(
        [Description("Absolute path to a .sln, .slnx or .csproj file, or a directory containing one.")]
        string path,
        CancellationToken cancellationToken) =>
        host.LoadAsync(path, cancellationToken);

    [McpServerTool(Name = "get_diagnostics")]
    [Description(ToolDescriptions.GetDiagnostics)]
    public Task<IssuePage> GetDiagnostics(
        [Description("Solution id returned by load_solution.")]
        string solutionId,
        CancellationToken cancellationToken,
        [Description("Optional absolute file or directory path that narrows the results.")]
        string? scope = null,
        [Description("Lowest severity to include: Info, Warning or Error.")]
        IssueSeverity minimum = IssueSeverity.Warning,
        [Description("Maximum issues to return.")]
        int limit = 50,
        [Description("Include diagnostics that only exist in generated code the caller cannot edit.")]
        bool includeGenerated = false) =>
        diagnostics.ReadAsync(solutionId, scope, minimum, limit, includeGenerated, cancellationToken);

    [McpServerTool(Name = "read_file")]
    [Description(ToolDescriptions.ReadFile)]
    public Task<FileView> ReadFile(
        [Description("Solution id returned by load_solution.")]
        string solutionId,
        [Description("Absolute path of the file to read.")]
        string file,
        CancellationToken cancellationToken,
        [Description("One-based line to start from.")]
        int startLine = 1,
        [Description("Maximum lines of text to return.")]
        int maxLines = 2000) =>
        reader.ReadAsync(solutionId, file, startLine, maxLines, cancellationToken);

    [McpServerTool(Name = "get_solution_overview")]
    [Description(ToolDescriptions.GetSolutionOverview)]
    public Task<SolutionOverview> GetSolutionOverview(
        [Description("Solution id returned by load_solution.")]
        string solutionId,
        CancellationToken cancellationToken,
        [Description("Maximum entries per section of the answer.")]
        int limit = 20) =>
        surveyor.OverviewAsync(solutionId, limit, cancellationToken);

    [McpServerTool(Name = "check_architecture")]
    [Description(ToolDescriptions.CheckArchitecture)]
    public Task<ArchitectureReport> CheckArchitecture(
        [Description("Solution id returned by load_solution.")]
        string solutionId,
        CancellationToken cancellationToken,
        [Description("Maximum findings to return.")]
        int limit = 50) =>
        surveyor.CheckAsync(solutionId, limit, cancellationToken);

    [McpServerTool(Name = "health_check")]
    [Description(ToolDescriptions.HealthCheck)]
    public Task<HealthReport> HealthCheck(
        [Description("Solution id returned by load_solution.")]
        string solutionId,
        CancellationToken cancellationToken) =>
        surveyor.HealthAsync(solutionId, cancellationToken);

    [McpServerTool(Name = "unload_solution")]
    [Description(ToolDescriptions.UnloadSolution)]
    public UnloadReport UnloadSolution(
        [Description("Solution id returned by load_solution.")]
        string solutionId) =>
        host.Unload(solutionId);
}
