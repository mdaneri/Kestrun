using System.Collections;
using Kestrun.Scheduling;
using Kestrun.Scripting;
using Serilog;
using Serilog.Core;
using Xunit;
using System.Management.Automation;
using JobInfo = Kestrun.Scheduling.JobInfo;

namespace KestrunTests.Scheduling;

public class SchedulerServiceTests
{
    private static Logger CreateLogger() => new LoggerConfiguration().MinimumLevel.Debug().CreateLogger();

    [Fact]
    public async Task Schedule_Interval_Runs_And_Updates_Timestamps()
    {
        using var pool = new KestrunRunspacePoolManager(1, 1);
        var log = CreateLogger();
        using var svc = new SchedulerService(pool, log, TimeZoneInfo.Utc);

        var ran = 0;
        svc.Schedule("tick", TimeSpan.FromMilliseconds(100), async ct =>
        {
            _ = Interlocked.Increment(ref ran);
            await Task.CompletedTask;
        }, runImmediately: false);

        await Task.Delay(350);
        var snap = svc.GetSnapshot();
        var job = Assert.Single(snap, j => j.Name == "tick");
        Assert.True(ran >= 2);
        _ = Assert.NotNull(job.LastRunAt);
        Assert.True(job.NextRunAt > job.LastRunAt);
    }

    [Fact]
    public async Task Pause_And_Resume_Work()
    {
        using var pool = new KestrunRunspacePoolManager(1, 1);
        var log = CreateLogger();
        using var svc = new SchedulerService(pool, log, TimeZoneInfo.Utc);

        var ran = 0;
        var interval = TimeSpan.FromMilliseconds(100);
        svc.Schedule("p", interval, async ct => { _ = Interlocked.Increment(ref ran); await Task.CompletedTask; });
        // wait for at least one run
        var start = DateTime.UtcNow;
        while (ran == 0 && (DateTime.UtcNow - start) < TimeSpan.FromSeconds(2))
        {
            await Task.Delay(10);
        }
        Assert.True(ran > 0);

        // Pause and allow any in-flight execution to drain before taking baseline
        Assert.True(svc.Pause("p"));
        await Task.Delay(interval + TimeSpan.FromMilliseconds(50));
        var pausedBaseline = ran;
        await Task.Delay(interval * 3);
        Assert.True(ran <= pausedBaseline + 1); // allow at most one in-flight run after pause

        Assert.True(svc.Resume("p"));
        var waitStart = DateTime.UtcNow;
        while (ran <= pausedBaseline + 1 && (DateTime.UtcNow - waitStart) < TimeSpan.FromSeconds(3))
        {
            await Task.Delay(10);
        }
        Assert.True(ran > pausedBaseline + 1);
    }

    [Fact]
    public async Task Cancel_Removes_And_Stops()
    {
        using var pool = new KestrunRunspacePoolManager(1, 1);
        var log = CreateLogger();
        using var svc = new SchedulerService(pool, log, TimeZoneInfo.Utc);

        var ran = 0;
        svc.Schedule("c", TimeSpan.FromMilliseconds(100), async ct => { _ = Interlocked.Increment(ref ran); await Task.CompletedTask; });
        await Task.Delay(250);
        Assert.True(ran > 0);

        Assert.True(svc.Cancel("c"));
        var afterCancel = ran;
        await Task.Delay(250);
        Assert.Equal(afterCancel, ran);
    }

    [Fact]
    public void GetReport_And_Hashtable_Shape()
    {
        using var pool = new KestrunRunspacePoolManager(1, 1);
        var log = CreateLogger();
        using var svc = new SchedulerService(pool, log, TimeZoneInfo.Utc);

        svc.Schedule("r1", TimeSpan.FromMinutes(1), async ct => await Task.CompletedTask);
        svc.Schedule("r2", "*/5 * * * * *", async ct => await Task.CompletedTask);

        var rpt = svc.GetReport(TimeZoneInfo.Utc);
        Assert.True(rpt.GeneratedAt <= DateTimeOffset.UtcNow);
        Assert.Contains(rpt.Jobs, j => j.Name == "r1");
        Assert.Contains(rpt.Jobs, j => j.Name == "r2");

        var ht = svc.GetReportHashtable(TimeZoneInfo.Utc);
        Assert.True(ht.ContainsKey("GeneratedAt"));
        Assert.True(ht.ContainsKey("Jobs"));
        var jobsObj = ht["Jobs"];
        Assert.NotNull(jobsObj);
        var jobs = Assert.IsType<Hashtable[]>(jobsObj);
        Assert.Contains(jobs, o => o["Name"]?.ToString() == "r1");
    }

    [Fact]
    public async Task Schedule_PowerShell_Code_And_File()
    {
        using var pool = new KestrunRunspacePoolManager(1, 1);
        var log = CreateLogger();
        using var svc = new SchedulerService(pool, log, TimeZoneInfo.Utc);

        svc.Schedule("ps-inline", TimeSpan.FromMilliseconds(100), "$null | Out-Null", ScriptLanguage.PowerShell);

        var tmp = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tmp, "$x=1; $x | Out-Null");
            svc.Schedule("ps-file", TimeSpan.FromMilliseconds(100), new FileInfo(tmp), ScriptLanguage.PowerShell);
            await Task.Delay(250);

            var snap = svc.GetSnapshot();
            Assert.Contains(snap, j => j.Name == "ps-inline");
            Assert.Contains(snap, j => j.Name == "ps-file");
        }
        finally
        {
            try { File.Delete(tmp); } catch { }
        }
    }

    [Fact]
    public async Task Schedule_RunImmediately_CSharp_Interval_Fires()
    {
        using var pool = new KestrunRunspacePoolManager(1, 1);
        var log = CreateLogger();
        using var svc = new SchedulerService(pool, log, TimeZoneInfo.Utc);

        var ran = 0;
        svc.Schedule("immediate-int", TimeSpan.FromMinutes(5), async ct => { _ = Interlocked.Increment(ref ran); await Task.CompletedTask; }, runImmediately: true);

        var start = DateTime.UtcNow;
        while (ran == 0 && DateTime.UtcNow - start < TimeSpan.FromSeconds(2))
        {
            await Task.Delay(25);
        }
        Assert.True(ran > 0);
        var job = Assert.Single(svc.GetSnapshot(), j => j.Name == "immediate-int");
        _ = Assert.NotNull(job.LastRunAt);
    }

    [Fact]
    public async Task Schedule_Cron_CSharp_RunImmediately()
    {
        using var pool = new KestrunRunspacePoolManager(1, 1);
        var log = CreateLogger();
        using var svc = new SchedulerService(pool, log, TimeZoneInfo.Utc);

        var ran = 0;
        svc.Schedule("cron-cs", "* * * * * *", async ct => { _ = Interlocked.Increment(ref ran); await Task.CompletedTask; }, runImmediately: true);

        var start = DateTime.UtcNow;
        while (ran == 0 && DateTime.UtcNow - start < TimeSpan.FromSeconds(2))
        {
            await Task.Delay(25);
        }
        Assert.True(ran > 0);
        var job = Assert.Single(svc.GetSnapshot(), j => j.Name == "cron-cs");
        _ = Assert.NotNull(job.LastRunAt);
    }

    [Fact]
    public async Task Schedule_Cron_PowerShell_ScriptBlock()
    {
        using var pool = new KestrunRunspacePoolManager(1, 1);
        var log = CreateLogger();
        using var svc = new SchedulerService(pool, log, TimeZoneInfo.Utc);

        // Script block as PowerShell job
        var ranFlag = new ManualResetEventSlim();
        var script = ScriptBlock.Create("$global:__psCronRuns = ($global:__psCronRuns + 1); if(-not $global:__psCronRuns){$global:__psCronRuns=1}");
        svc.Schedule("ps-cron", "* * * * * *", script, runImmediately: true);

        // Poll snapshot for LastRunAt (runImmediately path)
        var start = DateTime.UtcNow;
        // Allow a little more time on slower CI / Linux environments; was 2s and proved flaky on .NET 9
        while (DateTime.UtcNow - start < TimeSpan.FromSeconds(3))
        {
            var snap = svc.GetSnapshot();
            if (snap.Any(j => j.Name == "ps-cron" && j.LastRunAt != null))
            {
                ranFlag.Set();
                break;
            }
            await Task.Delay(50);
        }
        Assert.True(ranFlag.IsSet, "PowerShell cron job did not run immediately");
    }

    [Fact]
    public async Task Schedule_Cron_PowerShell_Code_String()
    {
        using var pool = new KestrunRunspacePoolManager(1, 1);
        var log = CreateLogger();
        using var svc = new SchedulerService(pool, log, TimeZoneInfo.Utc);

        svc.Schedule("ps-code-cron", "* * * * * *", "$null | Out-Null", ScriptLanguage.PowerShell, runImmediately: true);

        var start = DateTime.UtcNow;
        var seen = false;
        while (!seen && DateTime.UtcNow - start < TimeSpan.FromSeconds(2))
        {
            seen = svc.GetSnapshot().Any(j => j.Name == "ps-code-cron" && j.LastRunAt != null);
            if (!seen)
            {
                await Task.Delay(50);
            }
        }
        Assert.True(seen);
    }

    [Fact]
    public async Task Schedule_File_Cron_PowerShell()
    {
        using var pool = new KestrunRunspacePoolManager(1, 1);
        var log = CreateLogger();
        using var svc = new SchedulerService(pool, log, TimeZoneInfo.Utc);

        var tmp = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tmp, "$x=42 # cron file");
            svc.Schedule("ps-file-cron", "* * * * * *", new FileInfo(tmp), ScriptLanguage.PowerShell, runImmediately: true);

            var start = DateTime.UtcNow;
            var seen = false;
            while (!seen && DateTime.UtcNow - start < TimeSpan.FromSeconds(2))
            {
                seen = svc.GetSnapshot().Any(j => j.Name == "ps-file-cron" && j.LastRunAt != null);
                if (!seen)
                {
                    await Task.Delay(50);
                }
            }
            Assert.True(seen);
        }
        finally
        {
            try { File.Delete(tmp); } catch { }
        }
    }

    [Fact]
    public async Task ScheduleAsync_File_Interval_And_Cron()
    {
        using var pool = new KestrunRunspacePoolManager(1, 1);
        var log = CreateLogger();
        using var svc = new SchedulerService(pool, log, TimeZoneInfo.Utc);

        var tmp1 = Path.GetTempFileName();
        var tmp2 = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tmp1, "$null | Out-Null # interval async");
            await File.WriteAllTextAsync(tmp2, "$null | Out-Null # cron async");
            await svc.ScheduleAsync("ps-file-int-async", TimeSpan.FromMinutes(10), new FileInfo(tmp1), ScriptLanguage.PowerShell, runImmediately: true);
            await svc.ScheduleAsync("ps-file-cron-async", "* * * * * *", new FileInfo(tmp2), ScriptLanguage.PowerShell, runImmediately: true);

            var start = DateTime.UtcNow;
            var seen1 = false; var seen2 = false;
            // CI (Linux, cold PowerShell runspace init) can exceed 3s for first immediate run; allow up to 8s.
            while (!(seen1 && seen2) && DateTime.UtcNow - start < TimeSpan.FromSeconds(8))
            {
                var snap = svc.GetSnapshot();
                seen1 = snap.Any(j => j.Name == "ps-file-int-async" && j.LastRunAt != null);
                seen2 = snap.Any(j => j.Name == "ps-file-cron-async" && j.LastRunAt != null);
                if (!(seen1 && seen2))
                {
                    await Task.Delay(50);
                }
            }
            Assert.True(seen1);
            Assert.True(seen2);
        }
        finally
        {
            try { File.Delete(tmp1); } catch { }
            try { File.Delete(tmp2); } catch { }
        }
    }

    [Fact]
    public void CancelAll_Removes_All_Tasks()
    {
        using var pool = new KestrunRunspacePoolManager(1, 1);
        var log = CreateLogger();
        using var svc = new SchedulerService(pool, log, TimeZoneInfo.Utc);

        svc.Schedule("a1", TimeSpan.FromMinutes(1), async ct => await Task.CompletedTask);
        svc.Schedule("a2", TimeSpan.FromMinutes(1), async ct => await Task.CompletedTask);
        Assert.Equal(2, svc.GetSnapshot().Count);
        svc.CancelAll();
        Assert.Empty(svc.GetSnapshot());
    }

    [Fact]
    public void Duplicate_Name_Throws()
    {
        using var pool = new KestrunRunspacePoolManager(1, 1);
        var log = CreateLogger();
        using var svc = new SchedulerService(pool, log, TimeZoneInfo.Utc);

        svc.Schedule("dup", TimeSpan.FromMinutes(1), async ct => await Task.CompletedTask);
        var ex = Assert.Throws<InvalidOperationException>(() => svc.Schedule("dup", TimeSpan.FromMinutes(1), async ct => await Task.CompletedTask));
        Assert.NotNull(ex);
    }

    [Fact]
    public void Invalid_Name_Operations_Throw()
    {
        using var pool = new KestrunRunspacePoolManager(1, 1);
        var log = CreateLogger();
        using var svc = new SchedulerService(pool, log, TimeZoneInfo.Utc);

        _ = Assert.Throws<ArgumentException>(() => svc.Cancel(" "));
        _ = Assert.Throws<ArgumentException>(() => svc.Pause(""));
        _ = Assert.Throws<ArgumentException>(() => svc.Resume(null!));
    }

    [Fact]
    public void Snapshot_Filtering_And_Hashtable_Output()
    {
        using var pool = new KestrunRunspacePoolManager(1, 1);
        var log = CreateLogger();
        using var svc = new SchedulerService(pool, log, TimeZoneInfo.Utc);

        svc.Schedule("job-alpha", TimeSpan.FromMinutes(1), async ct => await Task.CompletedTask);
        svc.Schedule("job-beta", TimeSpan.FromMinutes(1), async ct => await Task.CompletedTask);
        svc.Schedule("other", TimeSpan.FromMinutes(1), async ct => await Task.CompletedTask);

        var filtered = svc.GetSnapshot(TimeZoneInfo.Utc, asHashtable: false, "job-*");
        Assert.Equal(2, filtered.Count);
        Assert.All(filtered, o => Assert.StartsWith("job-", ((JobInfo)o).Name));

        var ht = svc.GetSnapshot(TimeZoneInfo.Utc, asHashtable: true, "other");
        var entry = Assert.Single(ht);
        var h = Assert.IsType<Hashtable>(entry);
        Assert.Equal("other", h["Name"]);
    }
}
