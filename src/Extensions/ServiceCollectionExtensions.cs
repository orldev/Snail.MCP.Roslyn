using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Snail.MCP.Roslyn.Adapters.Paths;
using Snail.MCP.Roslyn.Adapters.Workspaces;
using Snail.MCP.Roslyn.Configuration;
using Snail.MCP.Roslyn.Tools;
using Snail.MCP.Roslyn.Tools.Prompts;

namespace Snail.MCP.Roslyn.Extensions;

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddCodeAnalysis(IConfiguration configuration)
        {
            services.Configure<WorkspaceOptions>(configuration.GetSection(WorkspaceOptions.Section));
            services.AddSingleton<IPathMap, RootPathMap>();
            services.AddSingleton<SolutionCache>();
            services.AddSingleton<ISolutionHost, RoslynSolutionHost>();
            services.AddSingleton<IDiagnosticReader, RoslynDiagnostics>();
            services.AddSingleton<ISymbolLookup, RoslynSymbols>();
            services.AddSingleton<IReferenceFinder, RoslynReferences>();
            services.AddSingleton<IRenamePlanner, RoslynRenamer>();
            services.AddSingleton<IStructureRefactorer, RoslynStructure>();
            services.AddSingleton<IImpactAnalyzer, RoslynImpact>();
            services.AddSingleton<IHierarchyExplorer, RoslynHierarchy>();
            services.AddSingleton<ISolutionSurveyor, RoslynSurveyor>();
            services.AddSingleton<IContainerReader, RoslynContainer>();
            services.AddSingleton<ICodeUsageAuditor, RoslynUsageAuditor>();
            services.AddSingleton<IAnalyzerRunner, RoslynAnalyzers>();
            services.AddSingleton<ICodeValidator, RoslynValidator>();
            services.AddSingleton<IScriptRunner, RoslynScripts>();
            services.AddSingleton<IGeneratedCodeReader, RoslynGeneratedCode>();
            services.AddSingleton<IFileReader, RoslynFileReader>();
            services.AddSingleton<IComponentExtractor, RoslynExtractor>();

            return services;
        }

        public IServiceCollection AddMcpTransport()
        {
            services.AddMcpServer(server => server.ServerInstructions = ServerGuidance.Instructions)
                .WithStdioServerTransport()
                .ExplainFailures()
                .WithTools<SolutionTools>()
                .WithTools<SymbolTools>()
                .WithTools<RefactorTools>()
                .WithTools<AnalysisTools>()
                .WithTools<ArchitectureTools>()
                .WithTools<ScriptTools>();

            return services;
        }
    }
}
