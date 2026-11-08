using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Snail.MCP.Roslyn.Adapters.Paths;
using Snail.MCP.Roslyn.Adapters.Workspaces;
using Snail.MCP.Roslyn.Configuration;
using Snail.MCP.Roslyn.Tools;
using Snail.MCP.Roslyn.Tools.Prompts;
using Snail.Sdk.Mcp.Hosting;
using Snail.Sdk.Mcp.Hosting.Answers;
using Snail.Sdk.Mcp.Hosting.Discovery;
using Snail.Sdk.Mcp.Hosting.Sessions;
using Snail.Sdk.Mcp.Http;

namespace Snail.MCP.Roslyn.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>The variable that configures the folders this process serves, named to a session that has none.</summary>
    public const string RootsSetting = "SNAIL_MCP_ROSLYN_SESSION_ROOTS__0";

    extension(IServiceCollection services)
    {
        public IServiceCollection AddCodeAnalysis(IConfiguration configuration)
        {
            services.Configure<WorkspaceOptions>(configuration);
            services.AddSingleton<IPathMap, RootPathMap>();
            services.AddSingleton<SolutionCache>();
            services.AddSingleton<PackageRestore>();
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
        /// <remarks>
        /// The MCP SDK's own transport, which keeps no sessions, so the address — <c>?root=</c>, <c>?tool=</c> — is read
        /// from the query of every request.
        /// </remarks>
        public IServiceCollection AddMcpTransportAtAnAddress(IConfiguration settings)
        {
            services.AddMcpTools(settings).WithHttpTransport().WithSessionAddress();

            return services;
        }

        /// <summary>The server speaking stdio to the process that started it.</summary>
        public IServiceCollection AddMcpTransportAsAProgram(IConfiguration settings)
        {
            services.AddMcpTools(settings).WithStdioServerTransport();

            return services;
        }

        /// <summary>
        /// The tools and the filters every answer passes through, whichever transport carries them.
        /// </summary>
        /// <remarks>
        /// Which transport is wanted is said by calling for it, never inferred from a port number, so that
        /// a port left at zero cannot quietly turn a listening server into a silent one. A failure keeps the words it
        /// always had — the tool, then what went wrong — inside the envelope, with no hint: the failures of this server
        /// are its refusals ("not loaded", "outside this session"), and "try once more" would send the model round again.
        /// <c>run_tools</c> takes as many steps and brings back as much as this server's settings always allowed.
        /// </remarks>
        private IMcpServerBuilder AddMcpTools(IConfiguration settings)
        {
            var options = settings.Get<WorkspaceOptions>() ?? new WorkspaceOptions();

            return services.AddMcpServer(server =>
                {
                    server.ServerInfo = ServerIdentity.Of(typeof(ServiceCollectionExtensions).Assembly);
                    server.ServerInstructions = ServerGuidance.Instructions;
                })
                .ExplainFailures(failures =>
                {
                    failures.Error = (tool, error) => $"{tool}: {error.Message}";
                    failures.Hint = null;
                })
                .RefuseWhatWasNotGranted()
                .WithSessionRoots(Volumes, roots => roots.RootsSetting = RootsSetting)
                .BoundAnswers()
                .WithTools<SolutionTools>()
                .WithTools<SymbolTools>()
                .WithTools<RefactorTools>()
                .WithTools<AnalysisTools>()
                .WithTools<ArchitectureTools>()
                .WithTools<ScriptTools>()
                .WithToolDiscovery(discovery =>
                {
                    foreach (var (tool, words) in ToolMeanings.All)
                        discovery.Meanings[tool] = words;
                })
                .WithToolSequences(sequences =>
                {
                    sequences.MaxSteps = Math.Max(1, options.MaxRunSteps);
                    sequences.OutputBudget = options.MaxResponseChars;
                });
        }
    }

    /// <summary>The folders this process was configured to serve; empty when it was configured with none.</summary>
    /// <remarks>The same function confines the tools and turns sessions away at the door.</remarks>
    public static IReadOnlyList<string> Volumes(IServiceProvider services) =>
        services.GetRequiredService<IOptions<WorkspaceOptions>>().Value.SessionRoots;
}
