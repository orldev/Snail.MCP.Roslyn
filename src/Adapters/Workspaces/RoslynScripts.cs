using System.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.Extensions.Options;
using Snail.MCP.Roslyn.Configuration;

namespace Snail.MCP.Roslyn.Adapters.Workspaces;

internal sealed class RoslynScripts(SolutionCache cache, ScriptTally tally, IOptions<WorkspaceOptions> options)
    : IScriptRunner
{
    private const string DisabledNote =
        "Scripting is switched off for this server. Set Workspace:AllowScripts to enable it. "
        + "A script runs in this process with this process's rights: the container, or its absence, is the only boundary.";

    private const string RunawayNote =
        "It is still running in this process and cannot be stopped from inside it: a script that never awaits and "
        + "never checks the cancellation token owns its thread until it returns. Nothing else was affected, but its "
        + "CPU is not released. Restart the server if the process stays busy.";

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
        var context = await ScriptContext.CreateAsync(solution, cancellationToken);

        // Counted before the run, not after: what the process will never give back is the assembly the
        // compilation emits, and it is emitted whether the script then succeeds, throws or runs away.
        tally.Counted();

        var started = Stopwatch.GetTimestamp();
        var seconds = options.Value.ScriptTimeoutSeconds;

        var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(seconds));

        try
        {
            // Started on a thread of its own: EvaluateAsync compiles and runs the body synchronously up to the
            // first await, so calling it here would hold this request until the script chose to end.
            var evaluation = Task.Run(
                () => CSharpScript.EvaluateAsync<object?>(code, Options, context, typeof(ScriptContext), deadline.Token),
                CancellationToken.None);

            Abandonable(evaluation, deadline);

            if (await Task.WhenAny(evaluation, Task.Delay(TimeSpan.FromSeconds(seconds + 1), cancellationToken)) != evaluation)
                return new ScriptResult(false, null, context.Output, [Runaway(seconds)], Elapsed(started), [RunawayNote]);

            return new ScriptResult(true, Render(await evaluation), context.Output, [], Elapsed(started), [ScopeNote]);
        }
        catch (CompilationErrorException error)
        {
            return new ScriptResult(false, null, context.Output, [.. error.Diagnostics.Select(diagnostic => diagnostic.ToString())],
                Elapsed(started), ["The script did not compile. Globals available: Solution, Compilations, Print, Types, Sym, Refs, Impls, Callers."]);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new ScriptResult(false, null, context.Output,
                [$"The script did not finish within {seconds} seconds."], Elapsed(started), []);
        }
        catch (Exception error)
        {
            return new ScriptResult(false, null, context.Output, [error.Message], Elapsed(started), []);
        }
    }

    private static string Runaway(int seconds) =>
        $"The script did not finish within {seconds} seconds and did not stop when asked.";

    /// <summary>Lets the request return while the script runs on. Cancellation is cooperative: a script that never
    /// awaits and never checks the token cannot be stopped from inside this process, and hanging the call forever
    /// would take the server down with it. The task is left observed so its failure cannot reach the finalizer.</summary>
    private static void Abandonable(Task evaluation, CancellationTokenSource deadline) =>
        _ = evaluation.ContinueWith(
            completed =>
            {
                _ = completed.Exception;
                deadline.Dispose();
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);

    private static long Elapsed(long started) => (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds;

    private static string? Render(object? value) => value switch
    {
        null => null,
        string text => text,
        System.Collections.IEnumerable items => string.Join(Environment.NewLine, items.Cast<object?>().Take(200)),
        _ => value.ToString()
    };
}
