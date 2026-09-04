using System.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.Extensions.Options;
using Snail.MCP.Roslyn.Configuration;

namespace Snail.MCP.Roslyn.Adapters.Workspaces;

internal sealed class RoslynScripts(SolutionCache cache, IOptions<WorkspaceOptions> options) : IScriptRunner
{
    private const string DisabledNote =
        "Scripting is switched off for this server. Set Workspace:AllowScripts to enable it. "
        + "A script runs in this process with this process's rights: the container, or its absence, is the only boundary.";

    private const string ScopeNote =
        "The script saw the solution as it stands on disk right now. It cannot write to the code: "
        + "the workspace is a snapshot in memory, and changes to it are discarded when the script ends.";

    private static readonly ScriptOptions Options = ScriptOptions.Default
        .WithReferences(
            typeof(object).Assembly,
            typeof(Enumerable).Assembly,
            typeof(Solution).Assembly,
            typeof(Compilation).Assembly,
            typeof(CSharpCompilation).Assembly,
            typeof(SymbolFinder).Assembly,
            typeof(ScriptContext).Assembly)
        .WithImports(
            "System",
            "System.Linq",
            "System.Collections.Generic",
            "System.Threading.Tasks",
            "Microsoft.CodeAnalysis",
            "Microsoft.CodeAnalysis.CSharp",
            "Microsoft.CodeAnalysis.CSharp.Syntax",
            "Microsoft.CodeAnalysis.FindSymbols");

    public async Task<ScriptResult> RunAsync(string solutionId, string code, CancellationToken cancellationToken)
    {
        if (!options.Value.AllowScripts)
            return new ScriptResult(false, null, string.Empty, ["Scripting is disabled."], 0, [DisabledNote]);

        var solution = await cache.FreshAsync(solutionId, cancellationToken);
        var context = new ScriptContext(solution);
        var started = Stopwatch.GetTimestamp();

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(options.Value.ScriptTimeoutSeconds));

        try
        {
            var value = await CSharpScript.EvaluateAsync<object?>(code, Options, context, typeof(ScriptContext), deadline.Token);

            return new ScriptResult(true, Render(value), context.Output, [], Elapsed(started), [ScopeNote]);
        }
        catch (CompilationErrorException error)
        {
            return new ScriptResult(false, null, context.Output, [.. error.Diagnostics.Select(diagnostic => diagnostic.ToString())],
                Elapsed(started), ["The script did not compile. Globals available: Solution, Compilations, Print, Types, Sym, Refs, Impls, Callers."]);
        }
        catch (OperationCanceledException)
        {
            return new ScriptResult(false, null, context.Output,
                [$"The script did not finish within {options.Value.ScriptTimeoutSeconds} seconds."], Elapsed(started), []);
        }
        catch (Exception error)
        {
            return new ScriptResult(false, null, context.Output, [error.Message], Elapsed(started), []);
        }
    }

    private static long Elapsed(long started) => (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds;

    private static string? Render(object? value) => value switch
    {
        null => null,
        string text => text,
        System.Collections.IEnumerable items => string.Join(Environment.NewLine, items.Cast<object?>().Take(200)),
        _ => value.ToString()
    };
}
