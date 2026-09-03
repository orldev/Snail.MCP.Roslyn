using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis.Text;

namespace Snail.MCP.Roslyn.Adapters.Workspaces;

/// <summary>Finds documented Blazor render costs in markup text, so reading a component shows its whole picture.</summary>
/// <remarks>These are pattern matches, not compiler judgments: each names a cost Blazor documents, and every
/// finding carries the SNR prefix so it can never be mistaken for a compiler diagnostic. Whether the cost sits
/// on a hot path is the caller's judgment, and the note on the answer says so.</remarks>
internal static class RenderCosts
{
    private const string MissingKeyMessage =
        "A loop renders markup without @key: when the collection changes, Blazor matches items by position and "
        + "re-renders them all. Put @key on the top-level element or component inside the loop.";

    private const string LoopLambdaMessage =
        "An event handler lambda inside a loop creates a new delegate for every item on every render; "
        + "passed to a component, it forces that component to re-render each time.";

    private const string UnfixedCascadeMessage =
        "CascadingValue without IsFixed subscribes every consumer to change notifications. "
        + "If the value never changes, IsFixed=\"true\" removes the subscriptions.";

    private const int BlockCap = 200;

    private static readonly Regex Loop = new(
        @"^\s*@?(foreach|for|while)\s*\(", RegexOptions.None, TimeSpan.FromSeconds(1));

    private static readonly Regex OpeningTag = new(
        @"<[A-Za-z]", RegexOptions.None, TimeSpan.FromSeconds(1));

    private static readonly Regex LambdaHandler = new(
        @"@on[a-zA-Z]+\s*=\s*""[^""]*=>", RegexOptions.None, TimeSpan.FromSeconds(1));

    private static readonly Regex Cascade = new(
        @"<CascadingValue[\s>]", RegexOptions.None, TimeSpan.FromSeconds(1));

    public static IReadOnlyList<CodeIssue> Scan(SourceText text, string file)
    {
        List<string> lines = [.. text.Lines.Select(line => line.ToString())];
        var code = CodeRegions(lines);
        List<CodeIssue> found = [];

        for (var index = 0; index < lines.Count; index++)
        {
            if (code[index])
                continue;

            if (Loop.IsMatch(lines[index]))
                ScanLoop(lines, index, file, found);

            if (Cascade.IsMatch(lines[index]) && !Tag(lines, index).Contains("IsFixed", StringComparison.Ordinal))
                found.Add(Issue("SNR003", UnfixedCascadeMessage, file, index + 1));
        }

        return [.. found.DistinctBy(issue => (issue.Id, issue.Line)).OrderBy(issue => issue.Line)];
    }

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

    private static void ScanLoop(List<string> lines, int start, string file, List<CodeIssue> found)
    {
        var opened = false;
        var depth = 0;
        var hasTag = false;
        var hasKey = false;

        for (var index = start; index < lines.Count && index < start + BlockCap; index++)
        {
            opened |= lines[index].Contains('{');
            depth += lines[index].Count(symbol => symbol == '{') - lines[index].Count(symbol => symbol == '}');

            if (index > start)
            {
                hasTag |= OpeningTag.IsMatch(lines[index]);
                hasKey |= lines[index].Contains("@key", StringComparison.Ordinal);

                if (LambdaHandler.IsMatch(lines[index]))
                    found.Add(Issue("SNR002", LoopLambdaMessage, file, index + 1));
            }

            if (opened && depth <= 0)
                break;
        }

        if (hasTag && !hasKey)
            found.Add(Issue("SNR001", MissingKeyMessage, file, start + 1));
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

    private static CodeIssue Issue(string id, string message, string file, int line) =>
        new(id, IssueSeverity.Info, message, file, line, 1, 1);
}
