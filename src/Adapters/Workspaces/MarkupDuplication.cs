using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;

namespace Snail.MCP.Roslyn.Adapters.Workspaces;

/// <summary>Finds markup blocks the solution repeats, because a block repeated with different values is a
/// component waiting to be extracted.</summary>
/// <remarks>Lines are compared with attribute values and razor expressions masked, so two blocks that differ
/// only in data still match — that difference is exactly the parameter list of the extracted component.</remarks>
internal static class MarkupDuplication
{
    public const string Rule = "markup-duplication";

    private const int MinLines = 3;

    private const int NamedOccurrences = 5;

    private static readonly Regex Quoted = new("\"[^\"]*\"", RegexOptions.None, TimeSpan.FromSeconds(1));

    private static readonly Regex Expression = new(@"@\w[\w.]*", RegexOptions.None, TimeSpan.FromSeconds(1));

    private static readonly Regex Spaces = new(@"\s+", RegexOptions.None, TimeSpan.FromSeconds(1));

    public static async Task<IReadOnlyList<ArchitectureFinding>> FindAsync(
        Solution solution,
        IPathMap paths,
        int cap,
        CancellationToken cancellationToken)
    {
        var files = await CollectAsync(solution, cancellationToken);
        var groups = Repeats(files);

        List<ArchitectureFinding> findings = [];
        HashSet<(int File, int Position)> covered = [];

        foreach (var occurrences in groups)
        {
            if (findings.Count >= cap)
                break;

            if (occurrences.All(covered.Contains))
                continue;

            var length = Extend(files, occurrences);

            foreach (var (file, position) in occurrences)
            {
                for (var offset = 0; offset < length; offset++)
                    covered.Add((file, position + offset));
            }

            findings.Add(Finding(files, paths, occurrences, length));
        }

        return findings;
    }

    private static async Task<List<(string Path, List<(string Norm, int Line)> Lines)>> CollectAsync(
        Solution solution,
        CancellationToken cancellationToken)
    {
        List<(string, List<(string, int)>)> files = [];

        foreach (var project in solution.Projects)
        {
            foreach (var document in project.AdditionalDocuments)
            {
                if (!MarkupMap.IsMarkup(document.FilePath) || document.FilePath is not { } path)
                    continue;

                var text = await document.GetTextAsync(cancellationToken);
                List<string> raw = [.. text.Lines.Select(line => line.ToString())];
                var code = RenderCosts.CodeRegions(raw);
                List<(string, int)> kept = [];

                for (var index = 0; index < raw.Count; index++)
                {
                    if (code[index])
                        continue;

                    var norm = Normalize(raw[index]);

                    if (norm.Length > 0)
                        kept.Add((norm, index + 1));
                }

                if (kept.Count >= MinLines)
                    files.Add((path, kept));
            }
        }

        return files;
    }

    /// <summary>Window starts grouped by their normalized text, kept only when the window recurs and holds a tag.</summary>
    private static List<List<(int File, int Position)>> Repeats(
        List<(string Path, List<(string Norm, int Line)> Lines)> files)
    {
        Dictionary<string, List<(int, int)>> windows = new(StringComparer.Ordinal);

        for (var file = 0; file < files.Count; file++)
        {
            var lines = files[file].Lines;

            for (var position = 0; position + MinLines <= lines.Count; position++)
            {
                var slice = lines.Skip(position).Take(MinLines).Select(entry => entry.Norm);
                var key = string.Join('\n', slice);

                if (!key.Contains('<'))
                    continue;

                (windows.TryGetValue(key, out var starts) ? starts : windows[key] = []).Add((file, position));
            }
        }

        return
        [
            .. windows.Values
                .Where(starts => starts.Count > 1)
                .OrderBy(starts => starts[0].Item1)
                .ThenBy(starts => starts[0].Item2)
        ];
    }

    /// <summary>How far past the base window every occurrence keeps agreeing.</summary>
    private static int Extend(
        List<(string Path, List<(string Norm, int Line)> Lines)> files,
        List<(int File, int Position)> occurrences)
    {
        var length = MinLines;

        while (true)
        {
            string? expected = null;

            foreach (var (file, position) in occurrences)
            {
                var lines = files[file].Lines;

                if (position + length >= lines.Count)
                    return length;

                var norm = lines[position + length].Norm;
                expected ??= norm;

                if (!string.Equals(norm, expected, StringComparison.Ordinal))
                    return length;
            }

            length++;
        }
    }

    private static ArchitectureFinding Finding(
        List<(string Path, List<(string Norm, int Line)> Lines)> files,
        IPathMap paths,
        List<(int File, int Position)> occurrences,
        int length)
    {
        List<string> places =
        [
            .. occurrences
                .Take(NamedOccurrences)
                .Select(occurrence =>
                    $"{paths.ToCaller(files[occurrence.File].Path)}:{files[occurrence.File].Lines[occurrence.Position].Line}")
        ];

        var more = occurrences.Count - places.Count;
        var listed = more > 0 ? $"{string.Join(", ", places)} and {more} more" : string.Join(", ", places);

        var (firstFile, firstPosition) = occurrences[0];

        return new ArchitectureFinding(
            Rule,
            $"A block of {length} markup lines repeats {occurrences.Count} times: {listed}. "
            + "Repetition that differs only in values is a candidate to extract into a component.",
            paths.ToCaller(files[firstFile].Path),
            files[firstFile].Lines[firstPosition].Line);
    }

    private static string Normalize(string line)
    {
        var trimmed = line.Trim();

        if (trimmed is "{" or "}" || trimmed.Length == 0)
            return string.Empty;

        return Spaces.Replace(Expression.Replace(Quoted.Replace(trimmed, "\"\""), "@"), " ");
    }
}
