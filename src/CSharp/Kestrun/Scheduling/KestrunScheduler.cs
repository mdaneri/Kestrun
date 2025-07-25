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
using static Kestrun.Scheduling.JobFactory;

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
    public void Schedule(string name, string cron, ScriptBlock scriptblock, bool runImmediately = false)
    {
        JobConfig config = new(ScriptLanguage.PowerShell, scriptblock.ToString(), _log, _pool);
        var job = JobFactory.Create(config);
        Schedule(name, cron, job, runImmediately);
    }
    public void Schedule(string name, TimeSpan interval, ScriptBlock scriptblock, bool runImmediately = false)
    {
        JobConfig config = new(ScriptLanguage.PowerShell, scriptblock.ToString(), _log, _pool);
        var job = JobFactory.Create(config);
        Schedule(name, interval, job, runImmediately);
    }
    public void Schedule(string name, TimeSpan interval, string code, ScriptLanguage lang, bool runImmediately = false)
    {
        JobConfig config = new(lang, code, _log, _pool);
        var job = JobFactory.Create(config);
        Schedule(name, interval, job, runImmediately);
    }

    public void Schedule(string name, string cron, string code, ScriptLanguage lang, bool runImmediately = false)
    {
        JobConfig config = new(lang, code, _log, _pool);
        var job = JobFactory.Create(config);
        Schedule(name, cron, job, runImmediately);
    }

    public void Schedule(string name, TimeSpan interval, FileInfo fileInfo, ScriptLanguage lang, bool runImmediately = false)
    {
        JobConfig config = new(lang, string.Empty, _log, _pool);
        var job = JobFactory.Create(config, fileInfo);
        Schedule(name, interval, job, runImmediately);
    }

    public void Schedule(string name, string cron, FileInfo fileInfo, ScriptLanguage lang, bool runImmediately = false)
    {
        JobConfig config = new(lang, string.Empty, _log, _pool);
        var job = JobFactory.Create(config, fileInfo);
        Schedule(name, cron, job, runImmediately);
    }

    public async Task ScheduleAsync(string name, TimeSpan interval, FileInfo fileInfo, ScriptLanguage lang, bool runImmediately = false, CancellationToken ct = default)
    {
        JobConfig config = new(lang, string.Empty, _log, _pool);
        var job = await JobFactory.CreateAsync(config, fileInfo, ct);
        Schedule(name, interval, job, runImmediately);
    }

    public async Task ScheduleAsync(string name, string cron, FileInfo fileInfo, ScriptLanguage lang, bool runImmediately = false, CancellationToken ct = default)
    {
        JobConfig config = new(lang, string.Empty, _log, _pool);
        var job = await JobFactory.CreateAsync(config, fileInfo, ct);
        Schedule(name, cron, job, runImmediately);
    }
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

    public void Dispose()
    {
        CancelAll();
        _pool.Dispose();
        _log.Information("SchedulerService disposed");
    }

}
