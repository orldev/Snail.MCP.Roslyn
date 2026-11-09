using System.Diagnostics;
using Microsoft.Extensions.Options;
using Snail.MCP.Roslyn.Configuration;

namespace Snail.MCP.Roslyn.Adapters.Workspaces;

/// <summary>Restores the packages of a solution that has none, before anything tries to read it.</summary>
/// <remarks>
/// Nothing else was going to. The worker clones the task's copy and never builds it, and this server only
/// noticed that restore had not run and said so in a note — which left every answer about a fresh task
/// <c>Degraded</c>, and left the note to a model that could not act on it either. Now that the server runs
/// as a container of its own it has the SDK and the package cache under it, and is the only part of the
/// arrangement that both needs the restore and can perform it.
/// <para>
/// Run before the solution is opened, never after: MSBuild binds references as it opens, so a restore that
/// finished afterwards would leave the loaded solution exactly as unrestored as before.
/// </para>
/// </remarks>
internal sealed class PackageRestore(IOptions<WorkspaceOptions> options, ILogger<PackageRestore> log)
{
    /// <summary>What happened, for the report; null when the solution was already restored or the deployment forbids it.</summary>
    public async Task<string?> EnsureAsync(string entry, CancellationToken cancellationToken)
    {
        if (!options.Value.ShouldRestoreOnLoad || !HasUnrestoredProject(entry))
            return null;

        var seconds = Math.Max(1, options.Value.RestoreSeconds);
        var started = Stopwatch.GetTimestamp();

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(seconds));

        try
        {
            var (code, output) = await RunAsync(entry, deadline.Token);
            var elapsed = (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds;

            if (code == 0)
            {
                Log.Restored(log, entry, elapsed);

                return $"Packages were not restored, so this server restored them: 'dotnet restore' took {elapsed} ms.";
            }

            Log.RestoreExited(log, entry, code, output);

            return $"Packages are not restored and 'dotnet restore' failed with exit code {code}, so semantic answers "
                   + $"under-report until it succeeds: {Cut(output)}";
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            Log.RestoreTimedOut(log, entry, seconds);

            return $"Packages are not restored and 'dotnet restore' passed its limit of {seconds} s, so semantic "
                   + "answers under-report. Restore the solution outside this server, or raise SNAIL_MCP_ROSLYN_RESTORE_SECONDS.";
        }
        catch (Exception error)
        {
            Log.RestoreNotStarted(log, error, entry);

            return $"Packages are not restored and 'dotnet restore' could not be started, so semantic answers "
                   + $"under-report: {error.Message}";
        }
    }

    /// <summary>
    /// Whether any project under the entry lacks a restore of the packages it declares now.
    /// </summary>
    /// <remarks>
    /// Asked of the disk rather than of the loaded solution, because the answer is needed before the
    /// solution exists. One unrestored project is enough: restore is a whole-solution operation, and a
    /// partly restored tree is what a half-finished one leaves behind. The check is <see cref="ProjectRestore"/>'s,
    /// the one the report is made by: a copy of its own here looked beside each project alone, so a repository on
    /// the artifacts layout was restored on every load after the report had stopped calling it unrestored.
    /// </remarks>
    private static bool HasUnrestoredProject(string entry)
    {
        var root = Path.GetDirectoryName(entry);

        if (string.IsNullOrEmpty(root))
            return false;

        var projects = Directory.EnumerateFiles(root, "*.csproj", SearchOption.AllDirectories).Take(500);

        return projects.Any(project => !ProjectRestore.HasRun(project));
    }

    private static async Task<(int Code, string Output)> RunAsync(string entry, CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo("dotnet")
            {
                ArgumentList = { "restore", entry },
                WorkingDirectory = Path.GetDirectoryName(entry),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            }
        };

        process.Start();

        var output = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errors = process.StandardError.ReadToEndAsync(cancellationToken);

        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            Stop(process);

            throw;
        }

        return (process.ExitCode, $"{await output}\n{await errors}".Trim());
    }

    /// <summary>Kills the restore and whatever it started.</summary>
    /// <remarks>A process that ended on its own between the check and the kill needs nothing, so a kill that fails is
    /// ignored.</remarks>
    private static void Stop(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch (Exception)
        {
        }
    }

    private static string Cut(string output) =>
        output.ReplaceLineEndings(" ") is { Length: > 400 } long_ ? long_[..399] + "…" : output.ReplaceLineEndings(" ");
}
