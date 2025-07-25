namespace Kestrun.Scheduling;

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Cronos;
using System.Management.Automation;
using Serilog;
using System.Management.Automation.Runspaces;
using Serilog.Events;
using System.IO;
using System.Collections;
using System.Text.RegularExpressions;
using Kestrun.Utilities;

public sealed record JobInfo(string Name,
                             DateTimeOffset? LastRunAt,
                             DateTimeOffset NextRunAt,
                             bool IsSuspended);


/// Whole snapshot
public sealed record ScheduleReport(
    DateTimeOffset GeneratedAt,
    IReadOnlyList<JobInfo> Jobs);

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
        => Schedule(name, interval, ScriptToJob(script), runImmediately);

    public void Schedule(string name, string cronExpr,
        ScriptBlock script, bool runImmediately = false)
        => Schedule(name, cronExpr, ScriptToJob(script), runImmediately);

    public async Task ScheduleAsync(string name, TimeSpan interval,
        FileInfo fileInfo, bool runImmediately = false, CancellationToken ct = default)
        => Schedule(name, interval, await ScriptToJobAsync(fileInfo, ct), runImmediately);

    public async Task ScheduleAsync(string name, string cronExpr,
        FileInfo fileInfo, bool runImmediately = false, CancellationToken ct = default)
        => Schedule(name, cronExpr, await ScriptToJobAsync(fileInfo, ct), runImmediately);

    public async Task Schedule(string name, TimeSpan interval,
        FileInfo fileInfo, bool runImmediately = false, CancellationToken ct = default)
        => await ScheduleAsync(name, interval, fileInfo, runImmediately, ct);

    public async Task Schedule(string name, string cronExpr,
        FileInfo fileInfo, bool runImmediately = false, CancellationToken ct = default)
        => await ScheduleAsync(name, cronExpr, fileInfo, runImmediately, ct);

    /*────────── CONTROL ──────────*/
    public bool Cancel(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Task name cannot be null or empty.", nameof(name));
        _log.Information("Cancelling scheduler job {Name}", name);
        if (_tasks.TryRemove(name, out var task))
        {
            task.TokenSource.Cancel();
            _log.Information("Scheduler job {Name} cancelled", name);
            return true;
        }
        return false;
    }

    public void CancelAll()
    {
        foreach (var kvp in _tasks.Keys)
            Cancel(kvp);
    }


    public ScheduleReport GetReport(TimeZoneInfo? displayTz = null)
    {
        // default to Zulu
        var tz = displayTz ?? TimeZoneInfo.Utc;
        var now = DateTimeOffset.UtcNow;

        var jobs = _tasks.Values
            .Select(t =>
            {
                // store timestamps internally in UTC; convert only for the report
                DateTimeOffset? last = t.LastRunAt?.ToOffset(tz.GetUtcOffset(t.LastRunAt.Value));
                DateTimeOffset next = t.NextRunAt.ToOffset(tz.GetUtcOffset(t.NextRunAt));

                return new JobInfo(t.Name, last, next, t.IsSuspended);
            })
            .OrderBy(j => j.NextRunAt)
            .ToArray();

        return new ScheduleReport(now, jobs);
    }


    public System.Collections.Hashtable GetReportHashtable(TimeZoneInfo? displayTz = null)
    {
        var rpt = GetReport(displayTz);

        var jobsArray = rpt.Jobs
            .Select(j => new System.Collections.Hashtable
            {
                ["Name"] = j.Name,
                ["LastRunAt"] = j.LastRunAt,
                ["NextRunAt"] = j.NextRunAt,
                ["IsSuspended"] = j.IsSuspended
            })
            .ToArray();                       // powershell likes [] not IList<>

        return new System.Collections.Hashtable
        {
            ["GeneratedAt"] = rpt.GeneratedAt,
            ["Jobs"] = jobsArray
        };
    }


    public IReadOnlyCollection<JobInfo> GetSnapshot()
        => [.. _tasks.Values.Select(t => new JobInfo(t.Name, t.LastRunAt, t.NextRunAt, t.IsSuspended))];


    public IReadOnlyCollection<object> GetSnapshot(
       TimeZoneInfo? tz = null,
       bool asHashtable = false,
       params string[] nameFilter)
    {
        tz ??= TimeZoneInfo.Utc;

        bool Matches(string name)
        {
            if (nameFilter == null || nameFilter.Length == 0) return true;
            foreach (var pat in nameFilter)
                if (RegexUtils.IsGlobMatch(name, pat)) return true;
            return false;
        }

        // fast path: no filter, utc, typed objects
        if (nameFilter.Length == 0 && tz.Equals(TimeZoneInfo.Utc) && !asHashtable)
        {
            return [.. _tasks.Values.Select(t => (object)new JobInfo(t.Name, t.LastRunAt, t.NextRunAt, t.IsSuspended))];
        }

        var jobs = _tasks.Values
                         .Where(t => Matches(t.Name))
                         .Select(t =>
                         {
                             var last = t.LastRunAt?.ToOffset(tz.GetUtcOffset(t.LastRunAt ?? DateTimeOffset.UtcNow));
                             var next = t.NextRunAt.ToOffset(tz.GetUtcOffset(t.NextRunAt));
                             return new JobInfo(t.Name, last, next, t.IsSuspended);
                         })
                         .OrderBy(j => j.NextRunAt)
                         .ToArray();

        if (!asHashtable) return jobs.Cast<object>().ToArray();

        // PowerShell-friendly shape
        return [.. jobs.Select(j => (object)new Hashtable
                {
                    ["Name"]        = j.Name,
                    ["LastRunAt"]   = j.LastRunAt,
                    ["NextRunAt"]   = j.NextRunAt,
                    ["IsSuspended"] = j.IsSuspended
                })];
    }


    public bool Pause(string name) => Suspend(name, true);
    public bool Resume(string name) => Suspend(name, false);

    /*────────── INTERNALS ──────────*/

    private bool Suspend(string name, bool suspend = true)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Task name cannot be null or empty.", nameof(name));

        if (_tasks.TryGetValue(name, out var task))
        {
            task.IsSuspended = suspend;
            _log.Information("Scheduler job {Name} {Action}", name, suspend ? "paused" : "resumed");
            return true;
        }
        return false;
    }

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
        var task = new ScheduledTask(name, job, cron, interval, runImmediately, cts)
        {
            NextRunAt = interval != null
                ? DateTimeOffset.UtcNow + interval.Value
                : (DateTimeOffset.UtcNow + NextCronDelay(cron!, _tz)).ToUniversalTime()
        };

        if (!_tasks.TryAdd(name, task))
            throw new InvalidOperationException($"A task named '{name}' already exists.");

        _ = Task.Run(() => LoopAsync(task), cts.Token);
        _log.Debug("Scheduled job '{Name}' (cron: {Cron}, interval: {Interval})", name, cron?.ToString(), interval);
    }

    private async Task LoopAsync(ScheduledTask task)
    {
        var ct = task.TokenSource.Token;

        if (task.RunImmediately && !task.IsSuspended)
            await SafeRun(task.Work, task, ct);

        while (!ct.IsCancellationRequested)
        {
            if (task.IsSuspended)
            {
                // sleep a bit while suspended, but stay responsive to Cancel()
                await Task.Delay(TimeSpan.FromSeconds(1), ct);
                continue;
            }

            TimeSpan delay = task.Interval ?? NextCronDelay(task.Cron!, _tz);
            if (delay < TimeSpan.Zero) delay = TimeSpan.Zero;

            try { await Task.Delay(delay, ct); }
            catch (TaskCanceledException) { break; }

            if (!ct.IsCancellationRequested)
                await SafeRun(task.Work, task, ct);
        }
    }

    private TimeSpan NextCronDelay(CronExpression expr, TimeZoneInfo tz)
    {
        var next = expr.GetNextOccurrence(DateTimeOffset.UtcNow, tz);
        if (next is null)
            _log.Warning("Cron expression {Expr} has no future occurrence", expr);
        return next.HasValue ? next.Value - DateTimeOffset.UtcNow : TimeSpan.MaxValue;
    }

    private async Task SafeRun(Func<CancellationToken, Task> work, ScheduledTask task, CancellationToken ct)
    {
        try
        {
            await work(ct);
            var lastRunAt = DateTimeOffset.UtcNow;
            task.LastRunAt = lastRunAt;
            // compute next run (only if still scheduled)
            if (task.Interval != null)
                task.NextRunAt = lastRunAt + task.Interval.Value;
            else if (task.Cron is not null)
                task.NextRunAt = task.Cron.GetNextOccurrence(lastRunAt, _tz) ?? DateTimeOffset.MaxValue;

        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { /* ignore */ }
        catch (Exception ex)
        {
            _log.Error(ex, "[Scheduler] Job '{Name}' failed", task.Name);
        }
    }
    private async Task<Func<CancellationToken, Task>> ScriptToJobAsync(FileInfo fileInfo, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(fileInfo);
        if (!fileInfo.Exists)
            throw new FileNotFoundException(fileInfo.FullName);

        string scriptText = await File.ReadAllTextAsync(fileInfo.FullName, ct);
        return ScriptToJob(scriptText);
    }

    private Func<CancellationToken, Task> ScriptToJob(ScriptBlock scriptBlock)
    {
        return ScriptToJob(scriptBlock.ToString());
    }
    private Func<CancellationToken, Task> ScriptToJob(string code)
        => async ct =>
        {

            var runspace = _pool.Acquire();
            try
            {
                using PowerShell ps = PowerShell.Create();
                ps.Runspace = runspace;
                ps.AddScript(code);

                //    var psResults = await ps.InvokeAsync().ConfigureAwait(false);
                using var reg = ct.Register(() => ps.Stop());

                var psResults = await ps.InvokeAsync().WaitAsync(ct).ConfigureAwait(false);

                _log.Verbose($"PowerShell script executed with {psResults.Count} results.");
                if (_log.IsEnabled(LogEventLevel.Debug))
                {
                    _log.Debug("PowerShell script output:");
                    foreach (var r in psResults.Take(10))      // first 10 only
                        _log.Debug("   • {Result}", r);
                    if (psResults.Count > 10)
                        _log.Debug("   … {Count} more", psResults.Count - 10);
                }

                if (ps.HadErrors || ps.Streams.Error.Count != 0 || ps.Streams.Verbose.Count > 0 || ps.Streams.Debug.Count > 0 || ps.Streams.Warning.Count > 0 || ps.Streams.Information.Count > 0)
                {
                    _log.Verbose("PowerShell script completed with verbose/debug/warning/info messages.");
                    _log.Verbose(BuildError.Text(ps));
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, "[Scheduler] PowerShell job failed – {Preview}", code[..Math.Min(40, code.Length)]);
                throw;
            }
            finally
            {
                // Ensure we release the runspace back to the pool                 
                _pool.Release(runspace);

            }
        };


    public void Dispose()
    {
        CancelAll();
        _pool.Dispose();
        _log.Information("SchedulerService disposed");
    }

}
