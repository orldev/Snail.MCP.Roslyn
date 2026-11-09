using Microsoft.Build.Locator;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Snail.MCP.Roslyn.Extensions;
using Snail.Sdk.Mcp.Hosting.Sessions;

namespace Snail.MCP.Roslyn.Tests.Support;

/// <summary>Hosts the analysis ports in process and loads this repository once for the whole suite.</summary>
public sealed class AnalysisFixture : IAsyncLifetime, IDisposable
{
    /// <summary>Test classes build their fixtures in parallel, and MSBuild is registered once per process.</summary>
    private static readonly Lock MsBuildRegistration = new();

    private readonly ServiceProvider _provider;

    public AnalysisFixture()
        : this([])
    {
    }

    /// <summary>Settings a test needs to differ from the defaults, as configuration keys. Not public: a collection
    /// fixture may declare one public constructor only, and that one is the suite's.</summary>
    internal AnalysisFixture(IEnumerable<KeyValuePair<string, string?>> settings, ISessionAddress? address = null)
    {
        lock (MsBuildRegistration)
        {
            if (!MSBuildLocator.IsRegistered)
                MSBuildLocator.RegisterDefaults();
        }

        ServiceCollection services = [];
        services.AddLogging();
        services.AddCodeAnalysis(new ConfigurationBuilder().AddInMemoryCollection(settings).Build());

        services.AddSingleton(address ?? SessionAddress.None);
        services.AddSessionRoots(ServiceCollectionExtensions.Volumes, ServiceCollectionExtensions.Roots);

        _provider = services.BuildServiceProvider();
    }

    public string SolutionId { get; private set; } = string.Empty;

    internal WorkspaceReport Report { get; private set; } = null!;

    internal ISolutionHost Host => _provider.GetRequiredService<ISolutionHost>();

    internal IDiagnosticReader Diagnostics => _provider.GetRequiredService<IDiagnosticReader>();

    internal ISymbolLookup Symbols => _provider.GetRequiredService<ISymbolLookup>();

    internal IReferenceFinder References => _provider.GetRequiredService<IReferenceFinder>();

    internal IRenamePlanner Renames => _provider.GetRequiredService<IRenamePlanner>();

    internal IStructureRefactorer Structure => _provider.GetRequiredService<IStructureRefactorer>();

    internal IImpactAnalyzer Impact => _provider.GetRequiredService<IImpactAnalyzer>();

    internal IHierarchyExplorer Hierarchy => _provider.GetRequiredService<IHierarchyExplorer>();

    internal ISolutionSurveyor Surveyor => _provider.GetRequiredService<ISolutionSurveyor>();

    internal IContainerReader Container => _provider.GetRequiredService<IContainerReader>();

    internal ICodeUsageAuditor Auditor => _provider.GetRequiredService<ICodeUsageAuditor>();

    internal IAnalyzerRunner Analyzers => _provider.GetRequiredService<IAnalyzerRunner>();

    internal ICodeValidator Validator => _provider.GetRequiredService<ICodeValidator>();

    internal IScriptRunner Scripts => _provider.GetRequiredService<IScriptRunner>();

    internal IGeneratedCodeReader Generated => _provider.GetRequiredService<IGeneratedCodeReader>();

    internal IFileReader Reader => _provider.GetRequiredService<IFileReader>();

    internal IComponentExtractor Extractor => _provider.GetRequiredService<IComponentExtractor>();

    public async Task InitializeAsync()
    {
        Report = await Host.LoadAsync(RepositoryPaths.Root, CancellationToken.None);
        SolutionId = Report.Id;
    }

    /// <summary>Addresses a member by identifier, because a bare member name is ambiguous by design.</summary>
    internal async Task<SymbolQuery> MemberAsync(string name, string container)
    {
        var matches = await Symbols.SearchAsync(SolutionId, name, SymbolKindFilter.Any, 100, CancellationToken.None);

        var match = matches.First(candidate =>
            candidate.Name == name && candidate.Container?.EndsWith(container, StringComparison.Ordinal) == true);

        return new SymbolQuery(match.Id, null, null, 0, 0);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose() => _provider.Dispose();
}

[CollectionDefinition(nameof(AnalysisCollection))]
public sealed class AnalysisCollection : ICollectionFixture<AnalysisFixture>;
