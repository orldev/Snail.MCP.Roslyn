using System.Text.RegularExpressions;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Intermediate;
using Microsoft.CodeAnalysis.Text;

namespace Snail.MCP.Roslyn.Adapters.Workspaces;

/// <summary>Reads the structure of a markup file with the Razor compiler's own front-end: elements with their
/// exact spans and attributes, across line breaks, with comments and strings excluded by the grammar itself.</summary>
/// <remarks>The published parser stopped at 6.0 and keeps its syntax tree internal, so this adapter walks the
/// public intermediate tree instead; legacy .cshtml lowers markup to flat content without element nodes and
/// answers null there. A parse the front end reports an error on answers null as well: a tree missing whatever
/// it could not read is worse than no tree, and the caller's text scan takes over. Static markup outside any
/// code block is collapsed into a single block, so a plain &lt;div&gt; standing on its own is not an element here.</remarks>
internal static class RazorMarkup
{
    private static readonly RazorProjectEngine Engine = RazorProjectEngine.Create(
        RazorConfiguration.Default,
        RazorProjectFileSystem.Create("/"),
        _ => { });

    private static readonly ConditionalWeakTable<SourceText, Attempt> Parsed = [];

    private static readonly Attempt Nothing = new(null, null);

    /// <summary>C# that opens a repetition. The tree hands over the code of a block, without its '@'.</summary>
    private static readonly Regex Opens = new(
        @"^\s*(foreach|for|while)\s*\(", RegexOptions.None, TimeSpan.FromSeconds(1));

    /// <summary>One attribute of an element. Text is the source it occupies, value included; for an attribute
    /// written on a continuation line that text opens with the line break, so judge position by the element.</summary>
    internal sealed record ElementAttribute(string Name, int Line, int Column, string Text);

    /// <summary>One markup element: 1-based line, 0-based column and absolute index of its opening '&lt;'.</summary>
    internal sealed record Element(
        string TagName,
        int Line,
        int Column,
        int Start,
        int Length,
        IReadOnlyList<ElementAttribute> Attributes);

    /// <summary>One loop and the elements it renders, the nested ones included.</summary>
    internal sealed record Loop(int Line, IReadOnlyList<Element> Elements);

    /// <summary>Reads one markup file, once per version of its text.</summary>
    /// <remarks>Reading a component runs this from the outline, from the render-cost scan and again from a
    /// rename, and a Razor parse is not cheap. A SourceText is immutable, so the result keyed on it needs no
    /// invalidation: an edit produces a new text and the old entry dies with it.</remarks>
    public static IReadOnlyList<Element>? Parse(SourceText source, string fileName) =>
        Attempted(source, fileName).Elements;

    /// <summary>The loops this markup renders in, or null when it was not read. Each carries the elements the
    /// loop repeats, which is what a render-cost question is actually about.</summary>
    public static IReadOnlyList<Loop>? LoopsIn(SourceText source, string fileName) =>
        Attempted(source, fileName).Loops;

    private static Attempt Attempted(SourceText source, string fileName) =>
        Parsed.GetValue(source, text => Read(text.ToString(), fileName));

    private static Attempt Read(string text, string fileName)
    {
        if (!fileName.EndsWith(".razor", StringComparison.OrdinalIgnoreCase))
            return Nothing;

        try
        {
            var document = Engine.Process(
                RazorSourceDocument.Create(text, fileName),
                FileKinds.Component,
                Array.Empty<RazorSourceDocument>(),
                Array.Empty<TagHelperDescriptor>());

            var tree = document.GetDocumentIntermediateNode();

            // A directive newer than this parser does not throw, it produces an error and a tree missing
            // whatever it could not read. Handing that back as authoritative is worse than not parsing at all.
            if (tree.GetAllDiagnostics().Any(diagnostic => diagnostic.Severity == RazorDiagnosticSeverity.Error))
                return Nothing;

            List<Element> elements = [];
            Walk(tree, text, elements);

            return new Attempt(elements, Loops(tree, text));
        }
        catch
        {
            return Nothing;
        }
    }

    private sealed record Attempt(IReadOnlyList<Element>? Elements, IReadOnlyList<Loop>? Loops);

    /// <summary>The loops of the render method, each with the elements between the code that opens the block and
    /// the code that closes it.</summary>
    /// <remarks>Read from the tree rather than by counting braces down the lines: a brace inside a string, an
    /// attribute value or an expression is not a block, and the line scan could not tell the difference.</remarks>
    private static IReadOnlyList<Loop> Loops(IntermediateNode tree, string text)
    {
        List<Loop> loops = [];

        void Scan(IntermediateNode node)
        {
            List<IntermediateNode> children = [.. node.Children];

            for (var index = 0; index < children.Count; index++)
            {
                if (children[index] is not CSharpCodeIntermediateNode opener
                    || Code(opener) is not { } code
                    || !Opens.IsMatch(code))
                    continue;

                loops.Add(new Loop(
                    opener.Source is { } source ? source.LineIndex + 1 : 0,
                    Repeated(children, index, Depth(code), text)));
            }

            foreach (var child in node.Children)
                Scan(child);
        }

        Scan(tree);

        return loops;
    }

    /// <summary>Elements the block renders, from the opener to the code that brings the brace depth back down.</summary>
    private static IReadOnlyList<Element> Repeated(List<IntermediateNode> siblings, int opener, int depth, string text)
    {
        List<Element> inside = [];

        for (var index = opener + 1; index < siblings.Count && depth > 0; index++)
        {
            if (siblings[index] is CSharpCodeIntermediateNode code)
            {
                depth += Code(code) is { } written ? Depth(written) : 0;

                continue;
            }

            Walk(siblings[index], text, inside);
        }

        return inside;
    }

    private static string? Code(CSharpCodeIntermediateNode node)
    {
        var written = string.Concat(node.Children.OfType<IntermediateToken>().Select(token => token.Content));

        return string.IsNullOrWhiteSpace(written) ? null : written;
    }

    private static int Depth(string code) => code.Count(symbol => symbol == '{') - code.Count(symbol => symbol == '}');

    private static void Walk(IntermediateNode node, string text, List<Element> elements)
    {
        if (node is MarkupElementIntermediateNode { Source: { } source } element)
        {
            List<ElementAttribute> attributes =
            [
                .. element.Children
                    .OfType<HtmlAttributeIntermediateNode>()
                    .Where(attribute => attribute.Source is not null && attribute.AttributeName is not null)
                    .Select(attribute => new ElementAttribute(
                        attribute.AttributeName!,
                        attribute.Source!.Value.LineIndex + 1,
                        attribute.Source!.Value.CharacterIndex,
                        Slice(text, attribute.Source!.Value)))
            ];

            elements.Add(new Element(
                element.TagName,
                source.LineIndex + 1,
                source.CharacterIndex,
                source.AbsoluteIndex,
                source.Length,
                attributes));
        }

        foreach (var child in node.Children)
            Walk(child, text, elements);
    }

    private static string Slice(string text, SourceSpan span)
    {
        var start = Math.Clamp(span.AbsoluteIndex, 0, text.Length);
        var end = Math.Clamp(start + span.Length, start, text.Length);

        return text[start..end];
    }
}
