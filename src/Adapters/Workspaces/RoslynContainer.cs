using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Snail.MCP.Roslyn.Adapters.Workspaces;

internal sealed class RoslynContainer(IPathMap paths, SolutionCache cache) : IContainerReader
{
    private const string ByNameNote =
        "A registration whose service or implementation is written as a string or resolved by reflection "
        + "is invisible to rename_symbol: it will not be rewritten and it will not break the build. "
        + "Check these by hand after any rename.";

    private const string UnregisteredNote =
        "Interfaces listed under 'unregistered' are implemented in source but appear in no registration found here. "
        + "That is a claim about this solution only: registration through a helper, a module or an external assembly is not visible.";

    private static readonly string[] Lifetimes = ["Singleton", "Scoped", "Transient"];

    public async Task<RegistrationReport> ReadAsync(
        string solutionId,
        string? service,
        int limit,
        CancellationToken cancellationToken)
    {
        var solution = await cache.FreshAsync(solutionId, cancellationToken);
        var cap = Math.Clamp(limit, 1, 300);

        List<Registration> found = [];
        HashSet<string> registered = [];
        var total = 0;

        foreach (var project in solution.Projects.Where(project => project.Language == LanguageNames.CSharp))
        {
            foreach (var document in project.Documents)
            {
                if (document.FilePath is not { } file || SourcePaths.IsUnderOutput(file))
                    continue;

                await CollectAsync(document, file, service, cap, found, registered, cancellationToken, count => total += count);
            }
        }

        var unregistered = await UnregisteredAsync(solution, registered, cancellationToken);

        return new RegistrationReport(found, unregistered, total, total > found.Count, Notes(found, unregistered.Count));
    }

    private async Task CollectAsync(
        Document document,
        string file,
        string? service,
        int cap,
        List<Registration> found,
        HashSet<string> registered,
        CancellationToken cancellationToken,
        Action<int> counted)
    {
        var model = await document.GetSemanticModelAsync(cancellationToken);
        var root = await document.GetSyntaxRootAsync(cancellationToken);
        var text = await document.GetTextAsync(cancellationToken);

        if (model is null || root is null)
            return;

        foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (model.GetSymbolInfo(invocation, cancellationToken).Symbol is not IMethodSymbol method)
                continue;

            if (Lifetime(method) is not { } lifetime)
                continue;

            var (contract, implementation) = Types(method, invocation, model, cancellationToken);

            if (contract is null)
                continue;

            registered.Add(contract);
            counted(1);

            if (service is not null && !contract.Contains(service, StringComparison.OrdinalIgnoreCase))
                continue;

            if (found.Count >= cap)
                continue;

            var line = text.Lines.GetLineFromPosition(invocation.SpanStart);

            found.Add(new Registration(
                contract,
                implementation,
                lifetime,
                method.Name,
                paths.ToCaller(file),
                line.LineNumber + 1,
                line.ToString().Trim()));
        }
    }

    private static string? Lifetime(IMethodSymbol method)
    {
        if (!IsContainerCall(method))
            return null;

        foreach (var lifetime in Lifetimes)
        {
            if (method.Name.Contains(lifetime, StringComparison.Ordinal))
                return lifetime;
        }

        return method.Name is "AddHostedService" ? "Singleton" : null;
    }

    private static bool IsContainerCall(IMethodSymbol method) =>
        method.ContainingNamespace?.ToDisplayString().StartsWith("Microsoft.Extensions.DependencyInjection", StringComparison.Ordinal) == true;

    private static (string? Service, string? Implementation) Types(
        IMethodSymbol method,
        InvocationExpressionSyntax invocation,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        if (method.TypeArguments.Length > 0)
        {
            return (
                method.TypeArguments[0].ToDisplayString(),
                method.TypeArguments.Length > 1 ? method.TypeArguments[1].ToDisplayString() : null);
        }

        List<string> written = [];

        foreach (var argument in invocation.ArgumentList.Arguments)
        {
            if (argument.Expression is TypeOfExpressionSyntax typeOf
                && model.GetTypeInfo(typeOf.Type, cancellationToken).Type is { } type)
                written.Add(type.ToDisplayString());
        }

        return written.Count == 0 ? (null, null) : (written[0], written.Count > 1 ? written[1] : null);
    }

    private async Task<IReadOnlyList<SymbolMatch>> UnregisteredAsync(
        Solution solution,
        HashSet<string> registered,
        CancellationToken cancellationToken)
    {
        List<SymbolMatch> missing = [];
        HashSet<string> seen = [];

        foreach (var project in solution.Projects.Where(project => project.Language == LanguageNames.CSharp))
        {
            if (TestProjects.Is(project))
                continue;

            var compilation = await project.GetCompilationAsync(cancellationToken);

            if (compilation is null)
                continue;

            foreach (var type in Implemented(compilation))
            {
                var name = type.ToDisplayString();

                if (registered.Contains(name) || !seen.Add(name))
                    continue;

                missing.Add(SymbolFacts.Match(type, paths));
            }
        }

        return missing;
    }

    private static IEnumerable<INamedTypeSymbol> Implemented(Compilation compilation)
    {
        HashSet<INamedTypeSymbol> contracts = new(SymbolEqualityComparer.Default);

        foreach (var type in compilation.GetSymbolsWithName(_ => true, SymbolFilter.Type).OfType<INamedTypeSymbol>())
        {
            if (type.TypeKind == TypeKind.Interface || type.IsAbstract || !type.Locations.Any(location => location.IsInSource))
                continue;

            foreach (var contract in type.Interfaces.Where(contract => contract.Locations.Any(location => location.IsInSource)))
                contracts.Add(contract);
        }

        return contracts;
    }

    private static IReadOnlyList<string> Notes(IReadOnlyList<Registration> found, int unregistered)
    {
        List<string> notes = [ByNameNote];

        if (unregistered > 0)
            notes.Add(UnregisteredNote);

        if (found.Count == 0)
            notes.Add("No registrations were found. This solution may register services outside it, or through a helper this reader does not follow.");

        return notes;
    }
}
