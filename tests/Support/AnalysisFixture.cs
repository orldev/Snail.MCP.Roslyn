using Microsoft.Build.Locator;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Snail.MCP.Roslyn.Extensions;

namespace Snail.MCP.Roslyn.Tests.Support;

/// <summary>Hosts the analysis ports in process and loads this repository once for the whole suite.</summary>
public sealed class AnalysisFixture : IAsyncLifetime, IDisposable
{
    private readonly ServiceProvider provider;

    public AnalysisFixture()
        : this([])
    {
    }

    /// <summary>Settings a test needs to differ from the defaults, as configuration keys. Not public: a collection
    /// fixture may declare one public constructor only, and that one is the suite's.</summary>
    internal AnalysisFixture(IEnumerable<KeyValuePair<string, string?>> settings)
    {
        if (!MSBuildLocator.IsRegistered)
            MSBuildLocator.RegisterDefaults();

        ServiceCollection services = [];
        services.AddLogging();
        services.AddCodeAnalysis(new ConfigurationBuilder().AddInMemoryCollection(settings).Build());

        provider = services.BuildServiceProvider();
    }

    public string SolutionId { get; private set; } = string.Empty;

    public WorkspaceReport Report { get; private set; } = null!;

    public ISolutionHost Host => provider.GetRequiredService<ISolutionHost>();

    public IDiagnosticReader Diagnostics => provider.GetRequiredService<IDiagnosticReader>();

    public ISymbolLookup Symbols => provider.GetRequiredService<ISymbolLookup>();

    public IReferenceFinder References => provider.GetRequiredService<IReferenceFinder>();

    public IRenamePlanner Renames => provider.GetRequiredService<IRenamePlanner>();

    public IStructureRefactorer Structure => provider.GetRequiredService<IStructureRefactorer>();

    public IImpactAnalyzer Impact => provider.GetRequiredService<IImpactAnalyzer>();

    public IHierarchyExplorer Hierarchy => provider.GetRequiredService<IHierarchyExplorer>();

    public ISolutionSurveyor Surveyor => provider.GetRequiredService<ISolutionSurveyor>();

    public IContainerReader Container => provider.GetRequiredService<IContainerReader>();

    public ICodeUsageAuditor Auditor => provider.GetRequiredService<ICodeUsageAuditor>();

    public IAnalyzerRunner Analyzers => provider.GetRequiredService<IAnalyzerRunner>();

    public ICodeValidator Validator => provider.GetRequiredService<ICodeValidator>();

    public IScriptRunner Scripts => provider.GetRequiredService<IScriptRunner>();

    public IGeneratedCodeReader Generated => provider.GetRequiredService<IGeneratedCodeReader>();

    public IFileReader Reader => provider.GetRequiredService<IFileReader>();

    public IComponentExtractor Extractor => provider.GetRequiredService<IComponentExtractor>();

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

    public void Dispose() => provider.Dispose();
}

[CollectionDefinition(nameof(AnalysisCollection))]
public sealed class AnalysisCollection : ICollectionFixture<AnalysisFixture>;
