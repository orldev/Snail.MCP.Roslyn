using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Snail.MCP.Roslyn.Adapters.Paths;
using Snail.MCP.Roslyn.Application;
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
            services.AddSingleton<SessionReach>();
            services.AddSingleton<ToolIndex>();
            services.AddSingleton<SolutionCache>();
            services.AddSingleton<ScriptTally>();
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

        /// <summary>The server answering sessions at an address, each naming the solution it works in.</summary>
        public IServiceCollection AddMcpTransportAtAnAddress()
        {
            services.AddHttpContextAccessor();
            services.AddSingleton<ISessionAddress, HttpAddress>();
            services.AddMcpTools().WithHttpTransport();

            return services;
        }

        /// <summary>The server speaking stdio to the process that started it.</summary>
        public IServiceCollection AddMcpTransportAsAProgram()
        {
            services.AddSingleton<ISessionAddress, StdioAddress>();
            services.AddMcpTools().WithStdioServerTransport();

            return services;
        }

        /// <summary>
        /// The tools and the two filters every answer passes through, whichever transport carries them.
        /// </summary>
        /// <remarks>
        /// Which transport is wanted is said by calling for it, never inferred from a port number, so that
        /// a port left at zero cannot quietly turn a listening server into a silent one.
        /// </remarks>
        private IMcpServerBuilder AddMcpTools()
        {
            return services.AddMcpServer(server => server.ServerInstructions = ServerGuidance.Instructions)
                .ExplainFailures()
                .BoundAnswers()
                .WithTools<SolutionTools>()
                .WithTools<SymbolTools>()
                .WithTools<RefactorTools>()
                .WithTools<AnalysisTools>()
                .WithTools<ArchitectureTools>()
                .WithTools<ScriptTools>()
                .WithTools<DiscoveryTools>()
                .WithTools<SequenceTools>();
        }
    }
}
