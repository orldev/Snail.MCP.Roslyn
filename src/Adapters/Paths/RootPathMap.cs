using Microsoft.Extensions.Options;
using Snail.MCP.Roslyn.Adapters.Workspaces;
using Snail.MCP.Roslyn.Configuration;

namespace Snail.MCP.Roslyn.Adapters.Paths;

/// <summary>Maps configured root pairs and falls through to identity when no root matches.</summary>
internal sealed class RootPathMap(IOptions<WorkspaceOptions> options) : IPathMap
{
    private readonly List<KeyValuePair<string, string>> _roots =
        [.. options.Value.Roots.OrderByDescending(root => root.Key.Length)];

    public string ToProcess(string callerPath) => Translate(callerPath, forward: true);

    public string ToCaller(string processPath) => Translate(processPath, forward: false);

    private string Translate(string path, bool forward)
    {
        if (string.IsNullOrWhiteSpace(path))
            return path;

        var full = Path.GetFullPath(path);

        foreach (var root in _roots)
        {
            var from = forward ? root.Key : root.Value;
            var to = forward ? root.Value : root.Key;

            if (!SourcePaths.IsUnder(full, from))
                continue;

            return Path.Combine(to, Path.GetRelativePath(from, full));
        }

        return full;
    }
}
