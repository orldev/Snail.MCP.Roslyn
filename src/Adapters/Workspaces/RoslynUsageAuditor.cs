using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;

namespace Snail.MCP.Roslyn.Adapters.Workspaces;

internal sealed class RoslynUsageAuditor(IPathMap paths, SolutionCache cache) : ICodeUsageAuditor
{
    private const string UnusedNote =
        "Each finding means: no reference to this declaration exists anywhere in the loaded solution. "
        + "That is not the same as dead. Reflection, dependency injection by name, serialisation "
        + "and anything outside the solution are invisible to the compiler. Verify before deleting. "
        + "Blazor markup is counted through the generated code, so a [Parameter] listed here is assigned in no "
        + "markup either — but an assignment through attribute splatting or DynamicComponent stays invisible.";

    private const string UncoveredNote =
        "Each finding means: no test project in this solution references this declaration. "
        + "Referencing is not testing — a referenced member may still be tested badly — but zero references is a hard signal.";

    private const string UnrestoredNote =
        "Package restore has not run for at least one project, so the compiler is missing references and every "
        + "count below is too low. Run 'dotnet restore' before acting on this list.";

    private const string NoTestsNote =
        "No test project was found in this solution, so every public declaration would be reported. "
        + "Load a solution that includes the tests, or take this result as inapplicable.";

    /// <summary>Attributes whose bearers stay auditable: the framework only writes these members, every read is
    /// compiler-visible, and markup assignments arrive through generated code the usage index already counts.</summary>
    private static readonly string[] ComponentAttributes =
    [
        "Microsoft.AspNetCore.Components.ParameterAttribute",
        "Microsoft.AspNetCore.Components.CascadingParameterAttribute",
        "Microsoft.AspNetCore.Components.EditorRequiredAttribute",
        "Microsoft.AspNetCore.Components.InjectAttribute",
        "Microsoft.AspNetCore.Components.SupplyParameterFromQueryAttribute",
        "Microsoft.AspNetCore.Components.SupplyParameterFromFormAttribute"
    ];

    private readonly SymbolResolver resolver = new(paths);

    public async Task<AuditPage> UnusedAsync(
        string solutionId,
        string? scope,
        int limit,
        CancellationToken cancellationToken)
    {
        var solution = await cache.FreshAsync(solutionId, cancellationToken);
        var usage = (await SolutionInsights.FactsAsync(solution, cancellationToken)).Usage;
        var markup = await MarkupMap.BuildAsync(solution, cancellationToken);
        var filter = string.IsNullOrWhiteSpace(scope) ? null : paths.ToProcess(scope);

        return Page(
            usage.Entries
                .Where(entry => entry.References == 0)
                .Where(entry => Auditable(entry.Symbol, filter, markup))
                .Select(entry => new AuditFinding(SymbolFacts.Match(entry.Symbol, paths), "no reference in the loaded solution")),
            limit,
            Caveats(solution, UnusedNote));
    }

    public async Task<AuditPage> UncoveredAsync(
        string solutionId,
        string? scope,
        int limit,
        CancellationToken cancellationToken)
    {
        var solution = await cache.FreshAsync(solutionId, cancellationToken);
        var tests = TestProjects.NamesIn(solution);

        if (tests.Count == 0)
            return new AuditPage([], 0, false, [NoTestsNote]);

        var usage = (await SolutionInsights.FactsAsync(solution, cancellationToken)).Usage;
        var markup = await MarkupMap.BuildAsync(solution, cancellationToken);
        var filter = string.IsNullOrWhiteSpace(scope) ? null : paths.ToProcess(scope);

        return Page(
            usage.Entries
                .Where(entry => !entry.Projects.Any(tests.Contains))
                .Where(entry => Auditable(entry.Symbol, filter, markup) && entry.Symbol.DeclaredAccessibility == Accessibility.Public)
                .Select(entry => new AuditFinding(
                    SymbolFacts.Match(entry.Symbol, paths),
                    entry.References == 0 ? "used nowhere, tests included" : "used only outside the test projects")),
            limit,
            Caveats(solution, UncoveredNote));
    }

    public async Task<IReadOnlyList<SymbolMatch>> TestsForAsync(
        string solutionId,
        SymbolQuery query,
        int limit,
        CancellationToken cancellationToken)
    {
        var solution = await cache.FreshAsync(solutionId, cancellationToken);
        var (symbol, _) = await resolver.PickAsync(solution, query, cancellationToken);

        if (symbol is null)
            return [];

        var tests = TestProjects.NamesIn(solution);
        var found = await SymbolFinder.FindReferencesAsync(symbol, solution, cancellationToken);
        var cap = Math.Clamp(limit, 1, 200);

        List<SymbolMatch> callers = [];
        HashSet<string> seen = [];

        foreach (var location in found
                     .SelectMany(referenced => referenced.Locations)
                     .Where(location => tests.Contains(location.Document.Project.Name)))
        {
            if (callers.Count >= cap)
                break;

            if (await ContainingAsync(location, cancellationToken) is not { } method)
                continue;

            var match = SymbolFacts.Match(method, paths);

            if (seen.Add(match.Id))
                callers.Add(match);
        }

        return callers;
    }

    private static async Task<ISymbol?> ContainingAsync(ReferenceLocation location, CancellationToken cancellationToken)
    {
        var model = await location.Document.GetSemanticModelAsync(cancellationToken);
        var root = model is null ? null : await model.SyntaxTree.GetRootAsync(cancellationToken);

        if (model is null || root is null)
            return null;

        for (var current = root.FindNode(location.Location.SourceSpan); current is not null; current = current.Parent)
        {
            if (model.GetDeclaredSymbol(current, cancellationToken) is IMethodSymbol method)
                return method;
        }

        return null;
    }

    /// <summary>A count taken against missing references is too low, and the answer must say so before anyone deletes.</summary>
    private static IReadOnlyList<string> Caveats(Solution solution, string note) =>
        ProjectRestore.Missing(solution).Count > 0 ? [UnrestoredNote, note] : [note];

    private static bool Auditable(ISymbol symbol, string? filter, IReadOnlyDictionary<string, string> markup)
    {
        if (symbol.IsImplicitlyDeclared || symbol.IsOverride || !AttributesAllowAuditing(symbol))
            return false;

        if (symbol.DeclaredAccessibility is Accessibility.NotApplicable)
            return false;

        if (symbol is IMethodSymbol { MethodKind: not MethodKind.Ordinary })
            return false;

        if (symbol.Name is "Main" or "" || symbol.ContainingType is { TypeKind: TypeKind.Interface })
            return false;

        if (Implements(symbol))
            return false;

        var file = symbol.Locations.FirstOrDefault(location => location.IsInSource)?.SourceTree?.FilePath;

        if (file is null)
            return false;

        if (SourcePaths.IsUnderOutput(file) && !markup.TryGetValue(file, out file))
            return false;

        return filter is null || SourcePaths.IsUnder(file, filter);
    }

    /// <summary>An attributed symbol is normally spared — frameworks reach it reflectively — except when every
    /// attribute it carries is a Blazor one whose uses the usage index genuinely sees.</summary>
    private static bool AttributesAllowAuditing(ISymbol symbol)
    {
        var attributes = symbol.GetAttributes();

        return attributes.Length == 0
            || attributes.All(attribute =>
                attribute.AttributeClass is { } owner && ComponentAttributes.Contains(owner.ToDisplayString()));
    }

    private static bool Implements(ISymbol symbol) =>
        symbol.ContainingType is { } owner
        && owner.AllInterfaces.SelectMany(contract => contract.GetMembers())
            .Any(member => SymbolEqualityComparer.Default.Equals(owner.FindImplementationForInterfaceMember(member), symbol));

    private static AuditPage Page(IEnumerable<AuditFinding> findings, int limit, IReadOnlyList<string> notes)
    {
        List<AuditFinding> all =
        [
            .. findings
                .OrderBy(finding => finding.Symbol.File, StringComparer.Ordinal)
                .ThenBy(finding => finding.Symbol.Line)
        ];

        var cap = Math.Clamp(limit, 1, 300);

        return new AuditPage([.. all.Take(cap)], all.Count, all.Count > cap, notes);
    }
}
