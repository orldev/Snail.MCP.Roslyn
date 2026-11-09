using System.Text;
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

    private readonly SymbolResolver _resolver = new(paths);
    private readonly SolutionEdits _editor = new(paths);
    private readonly TextScanner _scanner = new(paths);

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
            var textual = await _scanner.UnhandledAsync(solution, oldParts[^1], [], cancellationToken);

            return new EditPlan("rename_namespace", oldNamespace, [], [], textual, [], [], [DepthNote]);
        }

        var current = solution;
        var movedAlong = new SortedSet<string>(StringComparer.Ordinal);

        for (var index = 0; index < oldParts.Length; index++)
        {
            if (string.Equals(oldParts[index], newParts[index], StringComparison.Ordinal))
                continue;

            var segment = string.Join('.', newParts[..index].Append(oldParts[index]));

            if (await FindAsync(current, segment, cancellationToken) is not { } space)
                continue;

            movedAlong.UnionWith(await OutsideAsync(solution, string.Join('.', oldParts[..(index + 1)]), oldNamespace, cancellationToken));
            current = await Renamer.RenameSymbolAsync(current, space, new SymbolRenameOptions(), newParts[index], cancellationToken);
        }

        var edits = await _editor.BetweenAsync(solution, current, cancellationToken);

        if (updateFolders)
            edits = Realign(solution, edits, oldNamespace, newNamespace);

        var unhandled = await _scanner.UnhandledAsync(solution, oldParts[^1], SolutionEdits.Touched(edits), cancellationToken);
        var notes = Notes(unhandled.Count > 0, updateFolders);

        if (movedAlong.Count > 0)
            notes = [.. notes, SharedSegmentNote(movedAlong)];

        return new EditPlan(
            "rename_namespace",
            oldNamespace,
            edits,
            [],
            unhandled,
            await SolutionEdits.ConflictsAsync(solution, current, cancellationToken),
            [],
            notes);
    }

    /// <summary>The namespaces declared in source under <paramref name="ancestor"/> that lie outside <paramref name="target"/>, which renaming the ancestor's last segment moves too.</summary>
    /// <remarks>
    /// A namespace segment is one symbol for everything under it, so the compiler renames a middle segment for every
    /// namespace that shares it: renaming Snail.MCP.Vault to Snail.MCP.Obsidian for Snail.MCP.Vault.Configuration moves
    /// Snail.MCP.Vault.Tools as well. Each project holds its own part of the tree, so every C# project is read.
    /// </remarks>
    private static async Task<IReadOnlyList<string>> OutsideAsync(Solution solution, string ancestor, string target, CancellationToken cancellationToken)
    {
        var found = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var project in solution.Projects.Where(project => project.Language == LanguageNames.CSharp))
        {
            var compilation = await project.GetCompilationAsync(cancellationToken);

            if (compilation is null || Descend(compilation.Assembly.GlobalNamespace, ancestor.Split('.'), 0) is not { } space)
                continue;

            foreach (var name in Declared(space).Where(name => !IsWithinNamespace(name, target)))
                found.Add(name);
        }

        return [.. found];
    }

    private static IEnumerable<string> Declared(INamespaceSymbol space)
    {
        if (space.GetTypeMembers().Any(type => type.Locations.Any(location => location.IsInSource)))
            yield return space.ToDisplayString();

        foreach (var name in space.GetNamespaceMembers().SelectMany(Declared))
            yield return name;
    }

    private static string SharedSegmentNote(IEnumerable<string> movedAlong) =>
        "A middle segment of a namespace is shared by every namespace under it, so the compiler renames it for all of them: "
        + $"these move too — {string.Join(", ", movedAlong)}. Apply the plan only if that is wanted; otherwise move the types with move_type.";

    /// <summary>Plans moving a type into a file of its own, or into another file or namespace.</summary>
    /// <remarks>A file that holds nothing but the type makes the move a rename; only a changed namespace edits the
    /// file.</remarks>
    public async Task<EditPlan> MoveTypeAsync(
        string solutionId,
        SymbolQuery query,
        string? targetFile,
        string? targetNamespace,
        CancellationToken cancellationToken)
    {
        var solution = await cache.FreshAsync(solutionId, cancellationToken);
        var (symbol, candidates) = await _resolver.PickAsync(solution, query, SymbolFilter.Type, cancellationToken);

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

        var text = await document.GetTextAsync(cancellationToken);
        var space = targetNamespace ?? type.ContainingNamespace.ToDisplayString();
        var alone = root.DescendantNodes().OfType<BaseTypeDeclarationSyntax>().Count() == 1;

        List<FileEdit> edits = [];
        List<BlockReplacement> replacements = [];
        var after = solution;

        if (alone)
        {
            if (Renamespaced(text, root, space) is { } line)
            {
                after = solution.WithDocumentText(documentId, line.Rewritten);
                replacements.Add(line.Replacement with { File = paths.ToCaller(origin) });
            }

            edits.Add(new FileEdit(paths.ToCaller(origin), paths.ToCaller(destination), []));
        }
        else
        {
            var (removal, remainder) = Removed(text, declaration);
            var moved = Compose(root, declaration, space);
            var created = DocumentId.CreateNewId(document.Project.Id);

            after = solution
                .WithDocumentText(documentId, remainder)
                .AddDocument(created, Path.GetFileName(destination), SourceText.From(moved), filePath: destination);

            replacements.Add(removal with { File = paths.ToCaller(origin) });
            edits.Add(new FileEdit(paths.ToCaller(destination), null, [], moved));
        }

        return new EditPlan(
            "move_type",
            type.Name,
            edits,
            replacements,
            [],
            await SolutionEdits.ConflictsAsync(solution, after, cancellationToken),
            [],
            ["The type keeps its name, so references stay valid unless the namespace changed. Build once after applying."]);
    }

    /// <summary>The declaration cut out of its file, stated as the lines it occupies, its documentation comment
    /// and attributes included.</summary>
    private static (BlockReplacement Removal, SourceText Remainder) Removed(SourceText text, SyntaxNode declaration)
    {
        var first = text.Lines.GetLineFromPosition(declaration.FullSpan.Start);
        var last = text.Lines.GetLineFromPosition(declaration.Span.End);
        var region = TextSpan.FromBounds(first.Start, Math.Min(last.EndIncludingLineBreak, text.Length));

        return (new BlockReplacement(string.Empty, first.LineNumber + 1, last.LineNumber + 1, string.Empty),
            text.Replace(region, string.Empty));
    }

    /// <summary>The namespace line rewritten, or null when the namespace does not change.</summary>
    private static (BlockReplacement Replacement, SourceText Rewritten)? Renamespaced(
        SourceText text,
        SyntaxNode root,
        string space)
    {
        if (root.DescendantNodes().OfType<BaseNamespaceDeclarationSyntax>().FirstOrDefault() is not { } declared
            || string.Equals(declared.Name.ToString(), space, StringComparison.Ordinal))
            return null;

        var name = declared.Name.Span;
        var line = text.Lines.GetLineFromPosition(name.Start);

        var written = text.ToString(TextSpan.FromBounds(line.Start, name.Start))
            + space
            + text.ToString(TextSpan.FromBounds(name.End, line.End));

        return (
            new BlockReplacement(string.Empty, line.LineNumber + 1, line.LineNumber + 1, written),
            text.Replace(name, space));
    }

    public async Task<EditPlan> OrganizeUsingsAsync(string solutionId, string? scope, CancellationToken cancellationToken)
    {
        var solution = await cache.FreshAsync(solutionId, cancellationToken);
        var filter = string.IsNullOrWhiteSpace(scope) ? null : paths.ToProcess(scope);
        var after = solution;

        List<BlockReplacement> replacements = [];

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

                after = after.WithDocumentText(document.Id, organized.Rewritten);
                replacements.Add(organized.Replacement);
            }
        }

        return new EditPlan(
            "organize_usings",
            scope ?? "solution",
            [],
            replacements,
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
        var (symbol, candidates) = await _resolver.PickAsync(solution, query, cancellationToken);

        if (symbol is not IMethodSymbol method)
            return Empty("change_signature", query, candidates);

        if (method.DeclaringSyntaxReferences.FirstOrDefault() is not { } reference
            || await reference.GetSyntaxAsync(cancellationToken) is not MethodDeclarationSyntax declaration)
            return Empty("change_signature", query, [], "This method has no editable declaration in source.");

        var documentId = solution.GetDocumentIdsWithFilePath(reference.SyntaxTree.FilePath).FirstOrDefault();

        if (documentId is null || solution.GetDocument(documentId) is not { } document || document.FilePath is not { } file)
            return Empty("change_signature", query, [], "The declaring file is not part of the loaded solution.");

        if (Reshape(declaration, change) is not { } updated)
            return Empty("change_signature", query, [],
                "The requested signature does not parse as C#. Write each parameter as it would appear in the "
                + "declaration, modifiers, generic arguments and default values included.");

        var source = await document.GetTextAsync(cancellationToken);
        var (replacement, rewritten) = Reshaped(source, file, declaration, updated);
        var after = solution.WithDocumentText(documentId, rewritten);

        return new EditPlan(
            "change_signature",
            method.Name,
            [],
            [replacement],
            [],
            await SolutionEdits.ConflictsAsync(solution, after, cancellationToken),
            [],
            [SignatureNote]);
    }

    /// <summary>The signature rewritten in place: everything from the first modifier through the closing
    /// parenthesis, and not one line of the body, which the change does not touch.</summary>
    private (BlockReplacement Replacement, SourceText Rewritten) Reshaped(
        SourceText source,
        string file,
        MethodDeclarationSyntax declaration,
        MethodDeclarationSyntax updated)
    {
        var region = TextSpan.FromBounds(declaration.Span.Start, declaration.ParameterList.Span.End);
        var origin = updated.FullSpan.Start;
        var header = updated.ToFullString()[(updated.Span.Start - origin)..(updated.ParameterList.Span.End - origin)];

        var first = source.Lines.GetLineFromPosition(region.Start);
        var last = source.Lines.GetLineFromPosition(region.End);
        var indent = source.ToString(TextSpan.FromBounds(first.Start, region.Start));

        return (
            new BlockReplacement(paths.ToCaller(file), first.LineNumber + 1, last.LineNumber + 1, indent + header),
            source.Replace(region, header));
    }

    private async Task<(BlockReplacement Replacement, SourceText Rewritten)?> OrganizeAsync(
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

        return Rewritten(await document.GetTextAsync(cancellationToken), file, root, sorted);
    }

    /// <summary>The using block rewritten, stated as the lines it occupies and nothing below them.</summary>
    /// <remarks>Rewriting the block through the syntax tree and normalising whitespace reformats the whole
    /// file — brace style, blank lines, alignment — so organising three directives arrives as a diff over
    /// every line. Each directive is written back with its own trivia, so a comment above one travels with it.
    /// <para>The block ends at the last semicolon, not past its newline: the blank line under the block belongs to the
    /// file.</para></remarks>
    private (BlockReplacement Replacement, SourceText Rewritten) Rewritten(
        SourceText text,
        string file,
        CompilationUnitSyntax root,
        List<UsingDirectiveSyntax> sorted)
    {
        var first = root.Usings[0];
        var (header, attached) = SplitLeading(first.GetLeadingTrivia());
        var rebuilt = new StringBuilder(header.ToFullString());

        foreach (var directive in sorted)
        {
            var leading = directive == first ? attached : directive.GetLeadingTrivia();

            rebuilt.Append(directive.WithLeadingTrivia(leading).ToFullString());
        }

        var block = TextSpan.FromBounds(first.FullSpan.Start, root.Usings[^1].Span.End);
        var written = rebuilt.ToString().TrimEnd('\r', '\n');

        return (
            new BlockReplacement(
                paths.ToCaller(file),
                text.Lines.GetLineFromPosition(block.Start).LineNumber + 1,
                text.Lines.GetLineFromPosition(block.End).LineNumber + 1,
                written),
            text.Replace(block, written));
    }

    /// <summary>Trivia above the first directive that belongs to the file rather than to that directive:
    /// everything up to the last blank line. A licence header lives there and must not follow its directive
    /// down the sorted list.</summary>
    private static (SyntaxTriviaList Header, SyntaxTriviaList Attached) SplitLeading(SyntaxTriviaList leading)
    {
        var split = 0;

        for (var index = 1; index < leading.Count; index++)
        {
            if (leading[index].IsKind(SyntaxKind.EndOfLineTrivia) && leading[index - 1].IsKind(SyntaxKind.EndOfLineTrivia))
                split = index + 1;
        }

        return (SyntaxFactory.TriviaList(leading.Take(split)), SyntaxFactory.TriviaList(leading.Skip(split)));
    }

    private static string Key(UsingDirectiveSyntax directive) => directive.ToString();

    /// <summary>The declaration with the requested shape, or null when the request does not parse as C#.</summary>
    /// <remarks>The parameter list goes through the C# parser rather than being split on spaces. Splitting
    /// mangles every parameter whose text contains one that is not the separator: 'IDictionary&lt;string, int&gt; map'
    /// loses half its type argument, 'ref int x' takes 'ref' for a type, and 'int x = 5' takes '5' for a name.</remarks>
    private static MethodDeclarationSyntax? Reshape(MethodDeclarationSyntax declaration, SignatureChange change)
    {
        var updated = declaration;

        if (change.ReturnType is { } returnType)
        {
            var parsed = SyntaxFactory.ParseTypeName(returnType);

            if (parsed.ContainsDiagnostics)
                return null;

            updated = updated.WithReturnType(parsed.WithTrailingTrivia(SyntaxFactory.Space));
        }

        if (change.Parameters is not { } parameters)
            return updated;

        var list = SyntaxFactory.ParseParameterList($"({string.Join(", ", parameters)})");

        if (list.ContainsDiagnostics)
            return null;

        return updated.WithParameterList(list.WithTrailingTrivia(declaration.ParameterList.GetTrailingTrivia()));
    }

    /// <summary>The new file: the origin's usings, a file-scoped namespace, and the declaration exactly as
    /// its author wrote it. Only the wrapper is synthesised, so moving a type does not reformat it.</summary>
    private static string Compose(SyntaxNode root, SyntaxNode declaration, string space)
    {
        var composed = new StringBuilder();

        if (root is CompilationUnitSyntax unit)
        {
            foreach (var directive in unit.Usings)
                composed.AppendLine(directive.ToString());

            if (unit.Usings.Count > 0)
                composed.AppendLine();
        }

        composed.AppendLine($"namespace {space};").AppendLine();
        composed.AppendLine(declaration.ToFullString().Trim('\r', '\n'));

        return composed.ToString();
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

            if (!IsWithinNamespace(oldNamespace, prefix) || !IsWithinNamespace(newNamespace, prefix))
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
    private static bool IsWithinNamespace(string space, string prefix) =>
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
        new(operation, query.Id ?? query.Name ?? string.Empty, [], [], [], [], candidates,
            [note ?? (candidates.Count > 1
                ? "The name matches more than one symbol; pick one from candidates and call again by id."
                : "Symbol not found. Use search_symbols first.")]);
}
