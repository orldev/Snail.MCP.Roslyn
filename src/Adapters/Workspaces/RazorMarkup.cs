using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Intermediate;

namespace Snail.MCP.Roslyn.Adapters.Workspaces;

/// <summary>Reads the structure of a markup file with the Razor compiler's own front-end: elements with their
/// exact spans and attributes, across line breaks, with comments and strings excluded by the grammar itself.</summary>
/// <remarks>The published parser stopped at 6.0 and keeps its syntax tree internal, so this adapter walks the
/// public intermediate tree instead. A directive newer than the parser degrades a parse, never the server:
/// any failure returns null and the caller falls back to its text scan. Legacy .cshtml lowers markup to flat
/// content without element nodes, so it also answers null there.</remarks>
internal static class RazorMarkup
{
    private static readonly RazorProjectEngine Engine = RazorProjectEngine.Create(
        RazorConfiguration.Default,
        RazorProjectFileSystem.Create("/"),
        _ => { });

    internal sealed record ElementAttribute(string Name, int Line, int Column);

    /// <summary>One markup element: 1-based line, 0-based column and absolute index of its opening '&lt;'.</summary>
    internal sealed record Element(
        string TagName,
        int Line,
        int Column,
        int Start,
        int Length,
        IReadOnlyList<ElementAttribute> Attributes);

    public static IReadOnlyList<Element>? Parse(string text, string fileName)
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

            List<Element> elements = [];
            Walk(document.GetDocumentIntermediateNode(), elements);

            return elements;
        }
        catch
        {
            return null;
        }
    }

    private static void Walk(IntermediateNode node, List<Element> elements)
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
                        attribute.Source!.Value.CharacterIndex))
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
            Walk(child, elements);
    }
}
