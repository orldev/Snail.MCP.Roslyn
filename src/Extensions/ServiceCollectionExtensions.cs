using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Snail.MCP.Roslyn.Adapters.Paths;
using Snail.MCP.Roslyn.Adapters.Workspaces;
using Snail.MCP.Roslyn.Configuration;
using Snail.MCP.Roslyn.Tools;
using Snail.MCP.Roslyn.Tools.Prompts;
using Snail.Sdk.Mcp.Hosting;
using Snail.Sdk.Mcp.Hosting.Discovery;
using Snail.Sdk.Mcp.Hosting.Sessions;
using Snail.Sdk.Mcp.Http;

namespace Snail.MCP.Roslyn.Extensions;

internal static class ServiceCollectionExtensions
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
        /// a port left at zero cannot quietly turn a listening server into a silent one. Every tool answers in the
        /// envelope: a caller's mistake found deep inside — a solution not loaded, a path outside the session — is thrown
        /// as a refusal with its hint and type (<see cref="RefusalTypes"/>), and whatever else a tool throws is the
        /// server's own failure, answered with Snail.Sdk's "try once more" and logged with its trace.
        /// <c>run_tools</c> takes as many steps and brings back as much as this server's settings always allowed.
        /// Those settings are bound while the host is registered, before any host could refuse a value of the wrong type, so
        /// <see cref="ServerSettings.Bind(string, IConfiguration, Action{IConfiguration})"/> says it as a <see cref="StartRefusedException"/>
        /// naming the variable, where the binder crashed the process with a trace.
        /// </remarks>
        /// <exception cref="StartRefusedException">A setting holds a value its property's type cannot hold.</exception>
        private IMcpServerBuilder AddMcpTools(IConfiguration settings)
        {
            WorkspaceOptions? bound = null;

            ServerSettings.Bind(WorkspaceSettings.Name, settings, read => bound = read.Get<WorkspaceOptions>());

            var options = bound ?? new WorkspaceOptions();

            return services.AddMcpServer(server =>
                {
                    server.ServerInfo = ServerIdentity.Of(typeof(ServiceCollectionExtensions).Assembly);
                    server.ServerInstructions = ServerGuidance.Instructions;
                })
                .ExplainFailures()
                .WithSessionGrant()
                .WithSessionRoots(Volumes, Roots)
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

                    discovery.Examples = [.. ToolMeanings.Examples];
                })
                .WithToolSequences(sequences =>
                {
                    sequences.MaxSteps = Math.Max(1, options.MaxRunSteps);
                    sequences.OutputBudget = options.MaxResponseChars;
                });
        }
    }

    /// <summary>How a session's roots are read here: the setting named to a session that has none, a program configured with none reaching wherever its process can, and a session over HTTP reaching any copy it names.</summary>
    /// <remarks>
    /// A server started as a program for one caller — the shape a desktop client installs, with no settings at all — has
    /// always opened whatever solution its caller named. Over HTTP on loopback, configured with no volume, it serves the
    /// copy each session names (<c>?root=</c>), wherever it is: Snail.Sdk 0.5.0 turns such a session away unless the
    /// server says so. A session that names no root at a server configured with none is turned away at the door, before a
    /// call is made, and a configured volume — the image's <c>/workspaces</c> — confines every session to it.
    /// </remarks>
    public static void Roots(SessionRootsOptions roots)
    {
        roots.RootsSetting = RootsSetting;
        roots.ReachesEverywhereAsAProgram = true;
        roots.ReachesAnyNamedRoot = true;
    }

    /// <summary>The folders this process was configured to serve; empty when it was configured with none.</summary>
    /// <remarks>
    /// The same function confines the tools and turns sessions away at the door. A volume is the operator's setting, a
    /// folder of the machine this process runs on, so a leading <c>~</c> is this process's home: Snail.Sdk makes a volume
    /// absolute against the working directory and took <c>~/Projects</c> for a folder named <c>~</c> there. A path a
    /// caller names is not expanded so: over HTTP its <c>~</c> is the home of another machine.
    /// </remarks>
    public static IReadOnlyList<string> Volumes(IServiceProvider services) =>
    [
        .. services.GetRequiredService<IOptions<WorkspaceOptions>>().Value.SessionRoots
            .Select(volume => ServerFolders.ExpandHome(volume, volume))
    ];
}
