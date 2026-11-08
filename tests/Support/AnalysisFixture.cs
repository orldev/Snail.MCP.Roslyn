using Microsoft.Build.Locator;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Snail.MCP.Roslyn.Extensions;
using Snail.Sdk.Mcp.Hosting.Sessions;

namespace Snail.MCP.Roslyn.Tests.Support;

/// <summary>Hosts the analysis ports in process and loads this repository once for the whole suite.</summary>
public sealed class AnalysisFixture : IAsyncLifetime, IDisposable
{
    private readonly ServiceProvider _provider;

    public AnalysisFixture()
        : this([])
    {
    }

    /// <summary>Settings a test needs to differ from the defaults, as configuration keys. Not public: a collection
    /// fixture may declare one public constructor only, and that one is the suite's.</summary>
    internal AnalysisFixture(IEnumerable<KeyValuePair<string, string?>> settings, ISessionAddress? address = null)
    {
        if (!MSBuildLocator.IsRegistered)
            MSBuildLocator.RegisterDefaults();

        ServiceCollection services = [];
        services.AddLogging();
        services.AddCodeAnalysis(new ConfigurationBuilder().AddInMemoryCollection(settings).Build());

        services.AddSingleton(address ?? SessionAddress.None);
        services.AddMcpServer().WithSessionRoots(ServiceCollectionExtensions.Volumes);

        _provider = services.BuildServiceProvider();
    }

    public string SolutionId { get; private set; } = string.Empty;

    public WorkspaceReport Report { get; private set; } = null!;

    public ISolutionHost Host => _provider.GetRequiredService<ISolutionHost>();

    public IDiagnosticReader Diagnostics => _provider.GetRequiredService<IDiagnosticReader>();

    public ISymbolLookup Symbols => _provider.GetRequiredService<ISymbolLookup>();

    public IReferenceFinder References => _provider.GetRequiredService<IReferenceFinder>();

    public IRenamePlanner Renames => _provider.GetRequiredService<IRenamePlanner>();

    public IStructureRefactorer Structure => _provider.GetRequiredService<IStructureRefactorer>();

    public IImpactAnalyzer Impact => _provider.GetRequiredService<IImpactAnalyzer>();

    public IHierarchyExplorer Hierarchy => _provider.GetRequiredService<IHierarchyExplorer>();

    public ISolutionSurveyor Surveyor => _provider.GetRequiredService<ISolutionSurveyor>();

    public IContainerReader Container => _provider.GetRequiredService<IContainerReader>();

    public ICodeUsageAuditor Auditor => _provider.GetRequiredService<ICodeUsageAuditor>();

    public IAnalyzerRunner Analyzers => _provider.GetRequiredService<IAnalyzerRunner>();

    public ICodeValidator Validator => _provider.GetRequiredService<ICodeValidator>();

    public IScriptRunner Scripts => _provider.GetRequiredService<IScriptRunner>();

    public IGeneratedCodeReader Generated => _provider.GetRequiredService<IGeneratedCodeReader>();

    public IFileReader Reader => _provider.GetRequiredService<IFileReader>();

    public IComponentExtractor Extractor => _provider.GetRequiredService<IComponentExtractor>();

    public async Task InitializeAsync()
    {
        Report = await Host.LoadAsync(RepositoryPaths.Root, CancellationToken.None);
        SolutionId = Report.Id;
    }

    /// <summary>Addresses a member by identifier, because a bare member name is ambiguous by design.</summary>
    public async Task<SymbolQuery> MemberAsync(string name, string container)
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
