namespace Kestrun.Scheduling;

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Cronos;
using System.Management.Automation;
using Serilog;
using System.Management.Automation.Runspaces;

public sealed class SchedulerService : IDisposable
{
    private readonly ConcurrentDictionary<string, ScheduledTask> _tasks =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly KestrunRunspacePoolManager _pool;
    private readonly ILogger _log;
    private readonly TimeZoneInfo _tz;

    public SchedulerService(KestrunRunspacePoolManager pool, ILogger log, TimeZoneInfo? tz = null)
    {
        _pool = pool;
        _log = log;
        _tz = tz ?? TimeZoneInfo.Local;
    }

    /*────────── C# JOBS ──────────*/
    public void Schedule(string name, TimeSpan interval,
        Func<CancellationToken, Task> job, bool runImmediately = false)
        => ScheduleCore(name, job, cron: null, interval: interval, runImmediately);

    public void Schedule(string name, string cronExpr,
        Func<CancellationToken, Task> job, bool runImmediately = false)
    {
        var cron = CronExpression.Parse(cronExpr, CronFormat.IncludeSeconds);
        ScheduleCore(name, job, cron, null, runImmediately);
    }

    /*────────── PowerShell JOBS ──────────*/
    public void Schedule(string name, TimeSpan interval,
        ScriptBlock script, bool runImmediately = false)
        => Schedule(name, interval, WrapScriptBlock(script), runImmediately);

    public void Schedule(string name, string cronExpr,
        ScriptBlock script, bool runImmediately = false)
        => Schedule(name, cronExpr, WrapScriptBlock(script), runImmediately);

    public void Schedule(string name, TimeSpan interval,
        string filePath, bool runImmediately = false)
        => Schedule(name, interval, WrapFile(filePath), runImmediately);

    public void Schedule(string name, string cronExpr,
        string filePath, bool runImmediately = false)
        => Schedule(name, cronExpr, WrapFile(filePath), runImmediately);

    /*────────── CONTROL ──────────*/
    public bool Cancel(string name)
    {
        if (_tasks.TryRemove(name, out var task))
        {
            task.TokenSource.Cancel();
            return true;
        }
        return false;
    }

    public void CancelAll()
    {
        foreach (var kvp in _tasks.Keys)
            Cancel(kvp);
    }

    /*────────── INTERNALS ──────────*/
    private void ScheduleCore(
        string name,
        Func<CancellationToken, Task> job,
        CronExpression? cron,
        TimeSpan? interval,
        bool runImmediately)
    {
        if (cron is null && interval == null)
            throw new ArgumentException("Either cron or interval must be supplied.");

        var cts = new CancellationTokenSource();
        var task = new ScheduledTask(name, job, cron, interval, runImmediately, cts);

        if (!_tasks.TryAdd(name, task))
            throw new InvalidOperationException($"A task named '{name}' already exists.");

        _ = Task.Run(() => LoopAsync(task), cts.Token);
        _log.Debug("Scheduled job '{Name}' (cron: {Cron}, interval: {Interval})", name, cron?.ToString(), interval);
    }

    private async Task LoopAsync(ScheduledTask task)
    {
        var ct = task.TokenSource.Token;

        if (task.RunImmediately)
            await SafeRun(task.Work, task.Name, ct);

        while (!ct.IsCancellationRequested)
        {
            TimeSpan delay = task.Interval ?? NextCronDelay(task.Cron!, _tz);
            if (delay < TimeSpan.Zero) delay = TimeSpan.Zero;

            try { await Task.Delay(delay, ct); }
            catch (TaskCanceledException) { break; }

            if (!ct.IsCancellationRequested)
                await SafeRun(task.Work, task.Name, ct);
        }
    }

    private TimeSpan NextCronDelay(CronExpression expr, TimeZoneInfo tz)
    {
        var next = expr.GetNextOccurrence(DateTimeOffset.Now, tz);
        return next.HasValue ? next.Value - DateTimeOffset.Now : TimeSpan.FromDays(365);
    }

    private async Task SafeRun(Func<CancellationToken, Task> work, string name, CancellationToken ct)
    {
        try
        {
            await work(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { /* ignore */ }
        catch (Exception ex)
        {
            _log.Error(ex, "[Scheduler] Job '{Name}' failed", name);
        }
    }

    private Func<CancellationToken, Task> WrapScriptBlock(ScriptBlock script)
        => async ct =>
        {
            try
            {
                var runspace = _pool.Acquire();
                using PowerShell ps = PowerShell.Create();
                ps.Runspace = runspace;
                ps.AddScript(script.ToString());
                try
                {
                    var psResults = await ps.InvokeAsync().ConfigureAwait(false);

                    if (ps.HadErrors || ps.Streams.Error.Count != 0 || ps.Streams.Verbose.Count > 0 || ps.Streams.Debug.Count > 0 || ps.Streams.Warning.Count > 0 || ps.Streams.Information.Count > 0)
                    {
                        Log.Verbose("PowerShell script completed with verbose/debug/warning/info messages.");
                        Log.Verbose(BuildError.Text(ps));
                    }
                    //await Task.Factory.FromAsync(ps.BeginInvoke(), ps.EndInvoke);
                }
                finally
                {
                    // Ensure we release the runspace back to the pool
                    ps.Dispose();
                    _pool.Release(runspace);

                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, "[Scheduler] ScriptBlock execution failed");
                throw;
            }
        };

    private Func<CancellationToken, Task> WrapFile(string filePath)
    {
        if (!System.IO.File.Exists(filePath))
            throw new System.IO.FileNotFoundException(filePath);

        string scriptText = System.IO.File.ReadAllText(filePath);
        return async ct =>
        {
            using PowerShell ps = PowerShell.Create(_pool.Acquire());
            ps.AddScript(scriptText, useLocalScope: true);

            await Task.Factory.FromAsync(ps.BeginInvoke(), ps.EndInvoke);
        };
    }

    public void Dispose()
    {
        CancelAll();
        _pool.Dispose();
        _log.Information("SchedulerService disposed");
    }

}
