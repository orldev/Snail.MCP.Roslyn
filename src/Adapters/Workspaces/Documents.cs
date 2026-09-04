using Microsoft.CodeAnalysis;

namespace Snail.MCP.Roslyn.Adapters.Workspaces;

/// <summary>Finds a document by the path the caller names.</summary>
/// <remarks>Through the index the solution keeps rather than by scanning every project: markup is an additional
/// document and the index covers those too, so the scan cost the whole solution to answer about one file.</remarks>
internal static class Documents
{
    public static TextDocument? Find(Solution solution, string path)
    {
        foreach (var id in solution.GetDocumentIdsWithFilePath(path))
        {
            if ((solution.GetDocument(id) ?? (TextDocument?)solution.GetAdditionalDocument(id)) is { } document)
                return document;
        }

        return null;
    }

    /// <summary>The markup file at this path, or null when the path names something else.</summary>
    public static TextDocument? FindMarkup(Solution solution, string path) =>
        MarkupMap.IsMarkup(path) ? Find(solution, path) as AdditionalDocument : null;
}
