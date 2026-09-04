using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Intermediate;
using Microsoft.CodeAnalysis.Text;

namespace Snail.MCP.Roslyn.Adapters.Workspaces;

/// <summary>Reads the structure of a markup file with the Razor compiler's own front-end: elements with their
/// exact spans and attributes, across line breaks, with comments and strings excluded by the grammar itself.</summary>
/// <remarks>The published parser stopped at 6.0 and keeps its syntax tree internal, so this adapter walks the
/// public intermediate tree instead; legacy .cshtml lowers markup to flat content without element nodes and
/// answers null there. Razor diagnostics are not inspected, so only a thrown parse returns null: markup
/// degraded by a directive newer than 6.0 comes back as it stands, and the caller's text fallback never runs.</remarks>
internal static class RazorMarkup
{
    private static readonly RazorProjectEngine Engine = RazorProjectEngine.Create(
        RazorConfiguration.Default,
        RazorProjectFileSystem.Create("/"),
        _ => { });

    private static readonly ConditionalWeakTable<SourceText, Attempt> Parsed = [];

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

    /// <summary>Reads one markup file, once per version of its text.</summary>
    /// <remarks>Reading a component runs this from the outline, from the render-cost scan and again from a
    /// rename, and a Razor parse is not cheap. A SourceText is immutable, so the result keyed on it needs no
    /// invalidation: an edit produces a new text and the old entry dies with it.</remarks>
    public static IReadOnlyList<Element>? Parse(SourceText source, string fileName) =>
        Parsed.GetValue(source, text => new Attempt(Read(text.ToString(), fileName))).Elements;

    private static IReadOnlyList<Element>? Read(string text, string fileName)
    {
        if (!fileName.EndsWith(".razor", StringComparison.OrdinalIgnoreCase))
            return null;

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
                return null;

            List<Element> elements = [];
            Walk(tree, text, elements);

            return elements;
        }
        catch
        {
            return null;
        }
    }

    private sealed record Attempt(IReadOnlyList<Element>? Elements);

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
