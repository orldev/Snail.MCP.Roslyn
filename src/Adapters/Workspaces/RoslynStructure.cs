using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Rename;
using Microsoft.CodeAnalysis.Text;

namespace Snail.MCP.Roslyn.Adapters.Workspaces;

internal sealed class RoslynStructure(IPathMap paths, SolutionCache cache) : IStructureRefactorer
{
    private const string DepthNote =
        "Old and new namespaces have a different number of segments, which the compiler cannot rename as one step. "
        + "No edits are proposed. The occurrences listed are the complete textual picture: apply them yourself.";

    private const string FolderNote =
        "Folders were realigned with the new namespace. Moving a file changes which project item it belongs to, "
        + "so build once after applying before trusting any further answer.";

    private const string UnhandledNote =
        "Occurrences below are text the compiler does not treat as references: string literals, comments, "
        + "container registrations by name. They are listed, not changed.";

    private const string SignatureNote =
        "Only the declaration is edited. The compiler offers no cascading signature change, so every call site "
        + "that stops compiling is listed under 'conflicts' — that list is complete and is your work list.";

    private const string UsingsNote =
        "Directives the compiler reports as unnecessary were removed and the rest sorted, System first. "
        + "A directive needed only by a source generator's output can appear unnecessary here.";

    private readonly SymbolResolver resolver = new(paths);
    private readonly SolutionEdits editor = new(paths);
    private readonly TextScanner scanner = new(paths);

    public async Task<EditPlan> RenameNamespaceAsync(
        string solutionId,
        string oldNamespace,
        string newNamespace,
        bool updateFolders,
        CancellationToken cancellationToken)
    {
        var solution = await cache.FreshAsync(solutionId, cancellationToken);
        var oldParts = oldNamespace.Split('.');
        var newParts = newNamespace.Split('.');

        if (oldParts.Length != newParts.Length)
        {
            var textual = await scanner.UnhandledAsync(solution, oldParts[^1], [], cancellationToken);

            return new EditPlan("rename_namespace", oldNamespace, [], textual, [], [], [DepthNote]);
        }

        var current = solution;

        for (var index = 0; index < oldParts.Length; index++)
        {
            if (string.Equals(oldParts[index], newParts[index], StringComparison.Ordinal))
                continue;

            var prefix = string.Join('.', newParts[..index].Concat(oldParts[index..]));

            if (await FindAsync(current, prefix, cancellationToken) is not { } space)
                continue;

            current = await Renamer.RenameSymbolAsync(current, space, new SymbolRenameOptions(), newParts[index], cancellationToken);
        }

        var edits = await editor.BetweenAsync(solution, current, cancellationToken);

        if (updateFolders)
            edits = Realign(solution, edits, oldNamespace, newNamespace);

        var unhandled = await scanner.UnhandledAsync(solution, oldParts[^1], SolutionEdits.Touched(edits), cancellationToken);

        return new EditPlan(
            "rename_namespace",
            oldNamespace,
            edits,
            unhandled,
            await SolutionEdits.ConflictsAsync(solution, current, cancellationToken),
            [],
            Notes(unhandled.Count > 0, updateFolders));
    }

    public async Task<EditPlan> MoveTypeAsync(
        string solutionId,
        SymbolQuery query,
        string? targetFile,
        string? targetNamespace,
        CancellationToken cancellationToken)
    {
        var solution = await cache.FreshAsync(solutionId, cancellationToken);
        var (symbol, candidates) = await resolver.PickAsync(solution, query, cancellationToken);

        if (symbol is not INamedTypeSymbol type)
            return Empty("move_type", query, candidates);

        if (type.DeclaringSyntaxReferences.Length != 1)
            return Empty("move_type", query, [],
                "This type is declared in more than one place, so there is no single declaration to move. Split the partial declarations first.");

        var reference = type.DeclaringSyntaxReferences[0];
        var tree = reference.SyntaxTree;
        var documentId = solution.GetDocumentIdsWithFilePath(tree.FilePath).FirstOrDefault();

        if (documentId is null || solution.GetDocument(documentId) is not { } document || document.FilePath is not { } origin)
            return Empty("move_type", query, [], "The declaring file is not part of the loaded solution.");

        var declaration = await reference.GetSyntaxAsync(cancellationToken);
        var root = await document.GetSyntaxRootAsync(cancellationToken);

        if (root is null)
            return Empty("move_type", query, [], "The declaring file has no syntax tree.");

        var destination = targetFile is null
            ? Path.Combine(Path.GetDirectoryName(origin)!, $"{type.Name}.cs")
            : paths.ToProcess(targetFile);

        var alone = root.DescendantNodes().OfType<BaseTypeDeclarationSyntax>().Count() == 1;
        var moved = Compose(root, declaration, targetNamespace ?? type.ContainingNamespace.ToDisplayString());

        List<FileEdit> edits = [];
        var after = solution;

        if (alone)
        {
            after = solution.WithDocumentText(documentId, SourceText.From(moved));
            edits.Add(new FileEdit(paths.ToCaller(origin), paths.ToCaller(destination), [], moved));
        }
        else
        {
            var remainder = root.RemoveNode(declaration, SyntaxRemoveOptions.KeepNoTrivia)?.NormalizeWhitespace().ToFullString()
                ?? root.ToFullString();

            var created = DocumentId.CreateNewId(document.Project.Id);

            after = solution
                .WithDocumentText(documentId, SourceText.From(remainder))
                .AddDocument(created, Path.GetFileName(destination), SourceText.From(moved), filePath: destination);

            edits.Add(new FileEdit(paths.ToCaller(origin), null, [], remainder));
            edits.Add(new FileEdit(paths.ToCaller(destination), null, [], moved));
        }

        return new EditPlan(
            "move_type",
            type.Name,
            edits,
            [],
            await SolutionEdits.ConflictsAsync(solution, after, cancellationToken),
            [],
            ["The type keeps its name, so references stay valid unless the namespace changed. Build once after applying."]);
    }

    public async Task<EditPlan> OrganizeUsingsAsync(string solutionId, string? scope, CancellationToken cancellationToken)
    {
        var solution = await cache.FreshAsync(solutionId, cancellationToken);
        var filter = string.IsNullOrWhiteSpace(scope) ? null : paths.ToProcess(scope);
        var after = solution;

        List<FileEdit> edits = [];

        foreach (var project in solution.Projects.Where(project => project.Language == LanguageNames.CSharp))
        {
            var compilation = await project.GetCompilationAsync(cancellationToken);

            if (compilation is null)
                continue;

            HashSet<string> unnecessary =
            [
                .. compilation.GetDiagnostics(cancellationToken)
                    .Where(diagnostic => diagnostic.Id is "CS8019" or "IDE0005")
                    .Select(diagnostic => $"{diagnostic.Location.SourceTree?.FilePath}:{diagnostic.Location.SourceSpan.Start}")
            ];

            foreach (var document in project.Documents)
            {
                if (document.FilePath is not { } file || SourcePaths.IsUnderOutput(file))
                    continue;

                if (filter is not null && !SourcePaths.IsUnder(file, filter))
                    continue;

                if (await OrganizeAsync(document, file, unnecessary, cancellationToken) is not { } organized)
                    continue;

                after = after.WithDocumentText(document.Id, SourceText.From(organized));
                edits.Add(new FileEdit(paths.ToCaller(file), null, [], organized));
            }
        }

        return new EditPlan(
            "organize_usings",
            scope ?? "solution",
            edits,
            [],
            await SolutionEdits.ConflictsAsync(solution, after, cancellationToken),
            [],
            [UsingsNote]);
    }

    public async Task<EditPlan> ChangeSignatureAsync(
        string solutionId,
        SymbolQuery query,
        SignatureChange change,
        CancellationToken cancellationToken)
    {
        var solution = await cache.FreshAsync(solutionId, cancellationToken);
        var (symbol, candidates) = await resolver.PickAsync(solution, query, cancellationToken);

        if (symbol is not IMethodSymbol method)
            return Empty("change_signature", query, candidates);

        if (method.DeclaringSyntaxReferences.FirstOrDefault() is not { } reference
            || await reference.GetSyntaxAsync(cancellationToken) is not MethodDeclarationSyntax declaration)
            return Empty("change_signature", query, [], "This method has no editable declaration in source.");

        var documentId = solution.GetDocumentIdsWithFilePath(reference.SyntaxTree.FilePath).FirstOrDefault();

        if (documentId is null || solution.GetDocument(documentId) is not { } document || document.FilePath is not { } file)
            return Empty("change_signature", query, [], "The declaring file is not part of the loaded solution.");

        var updated = Reshape(declaration, change);
        var root = await document.GetSyntaxRootAsync(cancellationToken);

        if (root is null)
            return Empty("change_signature", query, [], "The declaring file has no syntax tree.");

        var text = root.ReplaceNode(declaration, updated).ToFullString();
        var after = solution.WithDocumentText(documentId, SourceText.From(text));

        return new EditPlan(
            "change_signature",
            method.Name,
            [new FileEdit(paths.ToCaller(file), null, [], text)],
            [],
            await SolutionEdits.ConflictsAsync(solution, after, cancellationToken),
            [],
            [SignatureNote]);
    }

    private static async Task<string?> OrganizeAsync(
        Document document,
        string file,
        HashSet<string> unnecessary,
        CancellationToken cancellationToken)
    {
        if (await document.GetSyntaxRootAsync(cancellationToken) is not CompilationUnitSyntax root || root.Usings.Count == 0)
            return null;

        List<UsingDirectiveSyntax> kept =
        [
            .. root.Usings.Where(directive => !unnecessary.Contains($"{file}:{directive.SpanStart}"))
        ];

        List<UsingDirectiveSyntax> sorted =
        [
            .. kept
                .OrderByDescending(directive => directive.Alias is null && directive.StaticKeyword.IsKind(SyntaxKind.None))
                .ThenByDescending(directive => directive.Name?.ToString().StartsWith("System", StringComparison.Ordinal) == true)
                .ThenBy(directive => directive.Name?.ToString(), StringComparer.Ordinal)
        ];

        if (kept.Count == root.Usings.Count && sorted.Select(Key).SequenceEqual(root.Usings.Select(Key), StringComparer.Ordinal))
            return null;

        return root.WithUsings([.. sorted]).NormalizeWhitespace().ToFullString();
    }

    private static string Key(UsingDirectiveSyntax directive) => directive.ToString();

    private static MethodDeclarationSyntax Reshape(MethodDeclarationSyntax declaration, SignatureChange change)
    {
        var updated = declaration;

        if (change.ReturnType is { } returnType)
            updated = updated.WithReturnType(SyntaxFactory.ParseTypeName(returnType).WithTrailingTrivia(SyntaxFactory.Space));

        if (change.Parameters is not { } parameters)
            return updated;

        var list = SyntaxFactory.ParameterList(SyntaxFactory.SeparatedList(
            parameters.Select(parameter => SyntaxFactory.Parameter(SyntaxFactory.Identifier(Name(parameter)))
                .WithType(SyntaxFactory.ParseTypeName(Type(parameter)).WithTrailingTrivia(SyntaxFactory.Space)))));

        return updated.WithParameterList(list);
    }

    private static string Type(string parameter) => parameter.Trim().Split(' ')[0];

    private static string Name(string parameter)
    {
        var parts = parameter.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);

        return parts.Length > 1 ? parts[^1] : "value";
    }

    private static string Compose(SyntaxNode root, SyntaxNode declaration, string space)
    {
        var usings = root is CompilationUnitSyntax unit ? unit.Usings : [];
        var body = SyntaxFactory.FileScopedNamespaceDeclaration(SyntaxFactory.ParseName(space))
            .WithMembers([(MemberDeclarationSyntax)declaration]);

        return SyntaxFactory.CompilationUnit()
            .WithUsings(usings)
            .WithMembers([body])
            .NormalizeWhitespace()
            .ToFullString();
    }

    private IReadOnlyList<FileEdit> Realign(
        Solution solution,
        IReadOnlyList<FileEdit> edits,
        string oldNamespace,
        string newNamespace)
    {
        List<FileEdit> aligned = [.. edits];

        foreach (var project in solution.Projects)
        {
            if (project.FilePath is not { } projectFile || Path.GetDirectoryName(projectFile) is not { } root)
                continue;

            var prefix = string.IsNullOrWhiteSpace(project.DefaultNamespace) ? project.Name : project.DefaultNamespace;

            if (!Namespaced(oldNamespace, prefix) || !Namespaced(newNamespace, prefix))
                continue;

            var from = Path.Combine([root, .. Segments(oldNamespace, prefix)]);
            var to = Path.Combine([root, .. Segments(newNamespace, prefix)]);

            if (!Directory.Exists(from))
                continue;

            foreach (var file in Directory.EnumerateFiles(from, "*.cs", SearchOption.AllDirectories))
            {
                if (SourcePaths.IsUnderOutput(file))
                    continue;

                var caller = paths.ToCaller(file);
                var moved = paths.ToCaller(Path.Combine(to, Path.GetRelativePath(from, file)));
                var index = aligned.FindIndex(edit => string.Equals(edit.File, caller, StringComparison.Ordinal));

                if (index >= 0)
                    aligned[index] = aligned[index] with { RenamedTo = moved };
                else
                    aligned.Add(new FileEdit(caller, moved, []));
            }
        }

        return aligned;
    }

    /// <summary>True when the namespace is the prefix or sits under it. 'Snail.Domain' must not swallow
    /// 'Snail.DomainModel', which a bare prefix test would.</summary>
    private static bool Namespaced(string space, string prefix) =>
        space.Equals(prefix, StringComparison.Ordinal) || space.StartsWith($"{prefix}.", StringComparison.Ordinal);

    private static string[] Segments(string space, string prefix) =>
        space[prefix.Length..].Trim('.').Split('.', StringSplitOptions.RemoveEmptyEntries);

    private static async Task<INamespaceSymbol?> FindAsync(Solution solution, string name, CancellationToken cancellationToken)
    {
        foreach (var project in solution.Projects.Where(project => project.Language == LanguageNames.CSharp))
        {
            var compilation = await project.GetCompilationAsync(cancellationToken);

            if (compilation is null)
                continue;

            var found = Descend(compilation.Assembly.GlobalNamespace, name.Split('.'), 0);

            if (found is not null)
                return found;
        }

        return null;
    }

    private static INamespaceSymbol? Descend(INamespaceSymbol current, string[] parts, int index)
    {
        if (index == parts.Length)
            return current;

        var next = current.GetNamespaceMembers().FirstOrDefault(member => member.Name.Equals(parts[index], StringComparison.Ordinal));

        return next is null ? null : Descend(next, parts, index + 1);
    }

    private static IReadOnlyList<string> Notes(bool unhandled, bool folders)
    {
        List<string> notes = [];

        if (unhandled)
            notes.Add(UnhandledNote);

        if (folders)
            notes.Add(FolderNote);

        return notes;
    }

    private static EditPlan Empty(
        string operation,
        SymbolQuery query,
        IReadOnlyList<SymbolMatch> candidates,
        string? note = null) =>
        new(operation, query.Id ?? query.Name ?? string.Empty, [], [], [], candidates,
            [note ?? (candidates.Count > 1
                ? "The name matches more than one symbol; pick one from candidates and call again by id."
                : "Symbol not found. Use search_symbols first.")]);
}
