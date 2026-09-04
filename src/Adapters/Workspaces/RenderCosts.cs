using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis.Text;

namespace Snail.MCP.Roslyn.Adapters.Workspaces;

/// <summary>Finds documented Blazor render costs in markup, so reading a component shows its whole picture.</summary>
/// <remarks>These are pattern matches, not compiler judgments: each names a cost Blazor documents, and every
/// finding carries the SNR prefix so it can never be mistaken for a compiler diagnostic. Whether the cost sits
/// on a hot path is the caller's judgment, and the note on the answer says so.</remarks>
internal static class RenderCosts
{
    private const string MissingKeyMessage =
        "A loop renders markup without @key: when the collection changes, Blazor matches items by position and "
        + "re-renders them all. Put @key on the top-level element or component inside the loop.";

    private const string LoopLambdaMessage =
        "This element takes a handler lambda inside a loop: a new delegate for every item on every render. "
        + "Passed to a component as a parameter, it also forces that component to re-render each time. "
        + "A method group, or a handler that takes the item as its argument, costs nothing per render.";

    private const string UnfixedCascadeMessage =
        "CascadingValue without IsFixed subscribes every consumer to change notifications. "
        + "If the value never changes, IsFixed=\"true\" removes the subscriptions.";

    private const string Cascading = "CascadingValue";

    private const int BlockCap = 200;

    private static readonly Regex Loop = new(
        @"^\s*@?(foreach|for|while)\s*\(", RegexOptions.None, TimeSpan.FromSeconds(1));

    private static readonly Regex OpeningTag = new(
        @"<[A-Za-z]", RegexOptions.None, TimeSpan.FromSeconds(1));

    private static readonly Regex LambdaHandler = new(
        @"@on[a-zA-Z]+\s*=\s*""[^""]*=>", RegexOptions.None, TimeSpan.FromSeconds(1));

    private static readonly Regex Cascade = new(
        @"<CascadingValue[\s>]", RegexOptions.None, TimeSpan.FromSeconds(1));

    public static IReadOnlyList<CodeIssue> Scan(SourceText text, string file) =>
        Ordered(
            RazorMarkup.LoopsIn(text, file) is { } loops && RazorMarkup.Parse(text, file) is { } elements
                ? FromTree(loops, elements, file)
                : FromText([.. text.Lines.Select(line => line.ToString())], file));

    /// <summary>Lines belonging to an @code or @functions block, where a loop is logic rather than rendering.</summary>
    public static bool[] CodeRegions(List<string> lines)
    {
        var code = new bool[lines.Count];
        var inside = false;
        var opened = false;
        var depth = 0;

        for (var index = 0; index < lines.Count; index++)
        {
            var trimmed = lines[index].TrimStart();

            if (!inside && (trimmed.StartsWith("@code", StringComparison.Ordinal)
                || trimmed.StartsWith("@functions", StringComparison.Ordinal)))
            {
                inside = true;
                opened = false;
                depth = 0;
            }

            if (!inside)
                continue;

            code[index] = true;
            opened |= lines[index].Contains('{');
            depth += lines[index].Count(symbol => symbol == '{') - lines[index].Count(symbol => symbol == '}');

            if (opened && depth <= 0)
                inside = false;
        }

        return code;
    }

    /// <summary>The three costs read off the parsed markup: the loops the grammar found, the elements each one
    /// repeats, and the attributes those elements carry.</summary>
    private static List<CodeIssue> FromTree(
        IReadOnlyList<RazorMarkup.Loop> loops,
        IReadOnlyList<RazorMarkup.Element> elements,
        string file)
    {
        List<CodeIssue> found = [];

        foreach (var loop in loops)
        {
            foreach (var element in loop.Elements.Where(Lambda))
                found.Add(Issue("SNR002", LoopLambdaMessage, file, element.Line));

            if (loop.Elements.Count > 0 && !loop.Elements.Any(Keyed))
                found.Add(Issue("SNR001", MissingKeyMessage, file, loop.Line));
        }

        foreach (var element in elements.Where(element =>
                     string.Equals(element.TagName, Cascading, StringComparison.Ordinal) && !Fixed(element)))
            found.Add(Issue("SNR003", UnfixedCascadeMessage, file, element.Line));

        return found;
    }

    private static bool Lambda(RazorMarkup.Element element) =>
        element.Attributes.Any(attribute => attribute.Text.Contains("=>", StringComparison.Ordinal));

    private static bool Keyed(RazorMarkup.Element element) =>
        element.Attributes.Any(attribute => attribute.Name == "@key");

    private static bool Fixed(RazorMarkup.Element element) =>
        element.Attributes.Any(attribute => attribute.Name.Equals("IsFixed", StringComparison.OrdinalIgnoreCase));

    /// <summary>The same three costs read off the text, for markup the grammar did not read.</summary>
    private static List<CodeIssue> FromText(List<string> lines, string file)
    {
        var code = CodeRegions(lines);
        List<CodeIssue> found = [];

        for (var index = 0; index < lines.Count; index++)
        {
            if (code[index])
                continue;

            if (Loop.IsMatch(lines[index]))
                ScanLoop(lines, index, BlockEnd(lines, index), file, found);

            if (Cascade.IsMatch(lines[index]) && !Tag(lines, index).Contains("IsFixed", StringComparison.Ordinal))
                found.Add(Issue("SNR003", UnfixedCascadeMessage, file, index + 1));
        }

        return found;
    }

    private static void ScanLoop(List<string> lines, int start, int end, string file, List<CodeIssue> found)
    {
        var hasTag = false;
        var hasKey = false;

        for (var index = start + 1; index <= end; index++)
        {
            hasTag |= OpeningTag.IsMatch(lines[index]);
            hasKey |= lines[index].Contains("@key", StringComparison.Ordinal);

            if (LambdaHandler.IsMatch(lines[index]))
                found.Add(Issue("SNR002", LoopLambdaMessage, file, index + 1));
        }

        if (hasTag && !hasKey)
            found.Add(Issue("SNR001", MissingKeyMessage, file, start + 1));
    }

    /// <summary>Index of the loop body's closing brace, or the end of the window it may occupy.</summary>
    private static int BlockEnd(List<string> lines, int start)
    {
        var opened = false;
        var depth = 0;

        for (var index = start; index < lines.Count && index < start + BlockCap; index++)
        {
            opened |= lines[index].Contains('{');
            depth += lines[index].Count(symbol => symbol == '{') - lines[index].Count(symbol => symbol == '}');

            if (opened && depth <= 0)
                return index;
        }

        return Math.Min(start + BlockCap, lines.Count) - 1;
    }

    /// <summary>The opening tag starting on this line, joined across the few lines it may span.</summary>
    private static string Tag(List<string> lines, int start)
    {
        var tag = string.Empty;

        for (var index = start; index < lines.Count && index < start + 10; index++)
        {
            tag += lines[index];

            if (lines[index].Contains('>'))
                break;
        }

        return tag;
    }

    private static IReadOnlyList<CodeIssue> Ordered(List<CodeIssue> found) =>
        [.. found.DistinctBy(issue => (issue.Id, issue.Line)).OrderBy(issue => issue.Line)];

    private static CodeIssue Issue(string id, string message, string file, int line) =>
        new(id, IssueSeverity.Info, message, file, line, 1, 1);
}
