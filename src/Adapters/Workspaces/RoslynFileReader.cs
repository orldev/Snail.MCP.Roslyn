using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Snail.MCP.Roslyn.Adapters.Workspaces;

internal sealed class RoslynFileReader(IPathMap paths, SolutionCache cache, IDiagnosticReader diagnostics) : IFileReader
{
    private const int IssueCap = 20;

    private const string UnrestoredNote =
        "Package restore has not run for at least one project in this solution, so the compiler is missing references "
        + "and diagnostics and use counts under-report. Run 'dotnet restore' and ask again.";

    private const string UsageNote =
        "Use counts cover the loaded solution only. Zero means no compiler-visible use there, not proof of dead code: "
        + "reflection, injection by name and serialisation are invisible to the compiler.";

    private const string NoSemanticsNote =
        "This file is not a C# document of the loaded solution, so only its text is returned: "
        + "no diagnostics, no declarations, no use counts.";

    private const string MarkupFileNote =
        "If this markup belongs to a solution, load that solution and read the file again: markup of a loaded "
        + "solution comes back with mapped diagnostics, its @code declarations and its component tags.";

    private const string MarkupViewNote =
        "This markup compiles into generated C#. Diagnostics and @code declarations are mapped back to their "
        + "authored lines; component tags are found by scanning the markup and resolved against the compilation. "
        + "A line of 0 means the symbol has no authored line: the component type is declared by the file as a whole.";

    private const string NoGeneratorNote =
        "The Razor generator produced no output for this markup file, so its diagnostics and @code declarations "
        + "are unknown here, not absent.";

    private const string RenderCostNote =
        "Issues with an SNR id are render-cost patterns matched on the markup text, not compiler diagnostics: "
        + "each names a documented Blazor cost, but whether it sits on a hot path is your judgment.";

    private static readonly Regex ComponentTag = new(
        @"<(?<name>[A-Z][A-Za-z0-9_]*(?:\.[A-Z][A-Za-z0-9_]*)*)[\s/>]",
        RegexOptions.Compiled,
        TimeSpan.FromSeconds(1));

    private static readonly string[] DirectiveWords =
    [
        "@page", "@using", "@namespace", "@inherits", "@implements", "@inject", "@layout", "@model",
        "@typeparam", "@attribute", "@rendermode", "@preservewhitespace"
    ];

    public async Task<FileView> ReadAsync(
        string solutionId,
        string file,
        int startLine,
        int maxLines,
        CancellationToken cancellationToken)
    {
        var solution = await cache.FreshAsync(solutionId, cancellationToken);
        var path = paths.ToProcess(file);
        var documentId = solution.GetDocumentIdsWithFilePath(path).FirstOrDefault();

        if (documentId is not null && solution.GetDocument(documentId) is { } document)
            return await SourceViewAsync(solution, solutionId, file, document, startLine, maxLines, cancellationToken);

        if (MarkupMap.IsMarkup(path) && FindMarkup(solution, path) is { } markup)
            return await MarkupViewAsync(solution, solutionId, file, markup, startLine, maxLines, cancellationToken);

        return await FromDiskAsync(path, startLine, maxLines, cancellationToken);
    }

    private async Task<FileView> SourceViewAsync(
        Solution solution,
        string solutionId,
        string file,
        Document document,
        int startLine,
        int maxLines,
        CancellationToken cancellationToken)
    {
        var text = await document.GetTextAsync(cancellationToken);
        var (rendered, start, end) = Slice(text.Lines, startLine, maxLines);

        return new FileView(
            paths.ToCaller(document.FilePath!),
            text.Lines.Count,
            start,
            end,
            rendered,
            await diagnostics.ReadAsync(solutionId, file, IssueSeverity.Warning, IssueCap, false, cancellationToken),
            await OutlineAsync(document, solution, cancellationToken),
            [],
            Notes(solution));
    }

    /// <summary>A .razor or .cshtml file of the solution: text, mapped diagnostics, @code members, tags and directives.</summary>
    private async Task<FileView> MarkupViewAsync(
        Solution solution,
        string solutionId,
        string file,
        TextDocument markup,
        int startLine,
        int maxLines,
        CancellationToken cancellationToken)
    {
        var text = await markup.GetTextAsync(cancellationToken);
        var (rendered, start, end) = Slice(text.Lines, startLine, maxLines);
        var compilation = await markup.Project.GetCompilationAsync(cancellationToken);
        var generated = compilation is null ? null : MarkupMap.FindGenerated(compilation, markup);
        var usage = await SolutionInsights.UsageAsync(solution, cancellationToken);

        List<FileSymbol> outline = [];

        if (compilation is not null && generated is not null)
            await AuthoredMembersAsync(compilation, generated, markup.FilePath!, usage, outline, cancellationToken);

        if (compilation is not null)
            TagsIn(compilation, text, markup.FilePath!, usage, outline);

        outline.Sort((left, right) => left.Line.CompareTo(right.Line));

        List<string> notes = [MarkupViewNote, .. Notes(solution)];

        if (generated is null)
            notes.Insert(0, NoGeneratorNote);

        var compiled = await diagnostics.ReadAsync(solutionId, file, IssueSeverity.Warning, IssueCap, false, cancellationToken);
        var costs = RenderCosts.Scan(text, paths.ToCaller(markup.FilePath!));

        if (costs.Count > 0)
            notes.Insert(0, RenderCostNote);

        return new FileView(
            paths.ToCaller(markup.FilePath!),
            text.Lines.Count,
            start,
            end,
            rendered,
            new IssuePage([.. compiled.Items, .. costs], compiled.Total + costs.Count, compiled.Truncated),
            outline,
            DirectivesIn(text),
            notes);
    }

    private static TextDocument? FindMarkup(Solution solution, string path) =>
        solution.Projects
            .SelectMany(project => project.AdditionalDocuments)
            .FirstOrDefault(document => string.Equals(document.FilePath, path, StringComparison.OrdinalIgnoreCase));

    /// <summary>The component type and the @code declarations that map back into the authored file.</summary>
    private async Task AuthoredMembersAsync(
        Compilation compilation,
        SyntaxTree generated,
        string authoredPath,
        SolutionUsage usage,
        List<FileSymbol> outline,
        CancellationToken cancellationToken)
    {
        var model = compilation.GetSemanticModel(generated);
        var root = await generated.GetRootAsync(cancellationToken);

        if (root.DescendantNodes().OfType<BaseTypeDeclarationSyntax>().FirstOrDefault() is { } declaration
            && model.GetDeclaredSymbol(declaration, cancellationToken) is { } component)
            outline.Add(Entry(component, 0, usage));

        foreach (var member in root.DescendantNodes().Where(IsDeclaration))
        {
            if (model.GetDeclaredSymbol(member, cancellationToken) is not { } declared || !Keeps(declared))
                continue;

            var span = MarkupMap.AuthoredSpan(member);

            if (!span.HasMappedPath || !string.Equals(span.Path, authoredPath, StringComparison.OrdinalIgnoreCase))
                continue;

            outline.Add(Entry(declared, span.StartLinePosition.Line + 1, usage));
        }
    }

    /// <summary>Component tags found in the markup, each resolved to a type the compilation knows. The Razor
    /// front-end reads them when it can — across line breaks, comments excluded — and a text scan fills in
    /// when it cannot (.cshtml, or markup the frozen parser no longer understands).</summary>
    private static void TagsIn(
        Compilation compilation,
        SourceText text,
        string fileName,
        SolutionUsage usage,
        List<FileSymbol> outline)
    {
        if (compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Components.IComponent") is not { } contract)
            return;

        HashSet<string> seen = [];

        if (RazorMarkup.Parse(text.ToString(), fileName) is { } elements)
        {
            foreach (var element in elements)
            {
                var name = element.TagName;

                if ((!char.IsUpper(name[0]) && !name.Contains('.')) || !seen.Add(name))
                    continue;

                if (ResolveComponent(compilation, name.Split('.')[^1], contract) is { } component)
                    outline.Add(Entry(component, element.Line, usage) with { Kind = "ComponentTag", Name = name });
            }

            return;
        }

        foreach (var line in text.Lines)
        {
            foreach (Match match in ComponentTag.Matches(line.ToString()))
            {
                var name = match.Groups["name"].Value;

                if (!seen.Add(name))
                    continue;

                if (ResolveComponent(compilation, name.Split('.')[^1], contract) is { } component)
                    outline.Add(Entry(component, line.LineNumber + 1, usage) with { Kind = "ComponentTag", Name = name });
            }
        }
    }

    private static FileSymbol Entry(ISymbol symbol, int line, SolutionUsage usage) =>
        new(
            SymbolFacts.IdOf(symbol),
            symbol.Name,
            symbol.Kind.ToString(),
            symbol.ContainingType?.ToDisplayString() ?? symbol.ContainingNamespace?.ToDisplayString(),
            symbol.DeclaredAccessibility.ToString(),
            line,
            usage.CountFor(symbol));

    private static INamedTypeSymbol? ResolveComponent(Compilation compilation, string name, INamedTypeSymbol contract)
    {
        foreach (var candidate in compilation.GetSymbolsWithName(name, SymbolFilter.Type).OfType<INamedTypeSymbol>())
        {
            if (Implements(candidate, contract))
                return candidate;
        }

        foreach (var assembly in compilation.SourceModule.ReferencedAssemblySymbols)
        {
            if (assembly.TypeNames.Contains(name) && Find(assembly.GlobalNamespace, name, contract) is { } found)
                return found;
        }

        return null;
    }

    private static INamedTypeSymbol? Find(INamespaceSymbol space, string name, INamedTypeSymbol contract)
    {
        foreach (var type in space.GetTypeMembers(name))
        {
            if (Implements(type, contract))
                return type;
        }

        foreach (var nested in space.GetNamespaceMembers())
        {
            if (Find(nested, name, contract) is { } found)
                return found;
        }

        return null;
    }

    private static bool Implements(INamedTypeSymbol type, INamedTypeSymbol contract) =>
        type.AllInterfaces.Contains(contract, SymbolEqualityComparer.Default);

    private static IReadOnlyList<MarkupDirective> DirectivesIn(SourceText text)
    {
        List<MarkupDirective> directives = [];

        foreach (var line in text.Lines)
        {
            var trimmed = line.ToString().Trim();

            if (DirectiveWords.Any(word =>
                trimmed.StartsWith(word, StringComparison.Ordinal)
                && (trimmed.Length == word.Length || char.IsWhiteSpace(trimmed[word.Length]))))
                directives.Add(new MarkupDirective(trimmed, line.LineNumber + 1));
        }

        return directives;
    }

    private async Task<FileView> FromDiskAsync(string path, int startLine, int maxLines, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"File '{paths.ToCaller(path)}' does not exist.");

        var text = SourceText.From(await File.ReadAllTextAsync(path, cancellationToken));
        var (rendered, start, end) = Slice(text.Lines, startLine, maxLines);

        List<string> notes = [NoSemanticsNote];

        if (MarkupMap.IsMarkup(path))
            notes.Add(MarkupFileNote);

        return new FileView(
            paths.ToCaller(path),
            text.Lines.Count,
            start,
            end,
            rendered,
            new IssuePage([], 0, false),
            [],
            [],
            notes);
    }

    private static (string Text, int Start, int End) Slice(TextLineCollection lines, int startLine, int maxLines)
    {
        var start = Math.Clamp(startLine, 1, Math.Max(lines.Count, 1));
        var end = Math.Min(start + Math.Clamp(maxLines, 1, 2000) - 1, lines.Count);

        var rendered = string.Join(
            '\n',
            Enumerable.Range(start, Math.Max(end - start + 1, 0)).Select(line => $"{line,6}\t{lines[line - 1]}"));

        return (rendered, start, Math.Max(end, start));
    }

    private async Task<IReadOnlyList<FileSymbol>> OutlineAsync(
        Document document,
        Solution solution,
        CancellationToken cancellationToken)
    {
        var model = await document.GetSemanticModelAsync(cancellationToken);
        var root = await document.GetSyntaxRootAsync(cancellationToken);

        if (model is null || root is null)
            return [];

        var usage = await SolutionInsights.UsageAsync(solution, cancellationToken);
        List<FileSymbol> outline = [];

        foreach (var declaration in root.DescendantNodes().Where(IsDeclaration))
        {
            if (model.GetDeclaredSymbol(declaration, cancellationToken) is not { } declared || !Keeps(declared))
                continue;

            var line = declaration.SyntaxTree.GetLineSpan(declaration.Span, cancellationToken).StartLinePosition.Line + 1;

            outline.Add(Entry(declared, line, usage));
        }

        return [.. outline.OrderBy(symbol => symbol.Line)];
    }

    private static bool IsDeclaration(SyntaxNode node) =>
        node is BaseTypeDeclarationSyntax or DelegateDeclarationSyntax or MethodDeclarationSyntax
            or PropertyDeclarationSyntax or EventDeclarationSyntax or VariableDeclaratorSyntax;

    private static bool Keeps(ISymbol symbol) =>
        symbol.Kind is SymbolKind.NamedType or SymbolKind.Method or SymbolKind.Property
            or SymbolKind.Field or SymbolKind.Event;

    private static IReadOnlyList<string> Notes(Solution solution)
    {
        List<string> notes = [UsageNote];

        if (ProjectRestore.Missing(solution).Count > 0)
            notes.Insert(0, UnrestoredNote);

        return notes;
    }
}
