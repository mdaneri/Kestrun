using System.Collections;
using Kestrun.Scheduling;
using Kestrun.Scripting;
using Serilog;
using Xunit;

namespace KestrunTests.Scheduling;

public class SchedulerServiceTests
{
    private static Serilog.ILogger CreateLogger() => new LoggerConfiguration().MinimumLevel.Debug().CreateLogger();

    [Fact]
    public async Task Schedule_Interval_Runs_And_Updates_Timestamps()
    {
        using var pool = new KestrunRunspacePoolManager(1, 1);
        var log = CreateLogger();
        using var svc = new SchedulerService(pool, log, TimeZoneInfo.Utc);

        int ran = 0;
        svc.Schedule("tick", TimeSpan.FromMilliseconds(100), async ct =>
        {
            Interlocked.Increment(ref ran);
            await Task.CompletedTask;
        }, runImmediately: false);

        await Task.Delay(350);
        var snap = svc.GetSnapshot();
        var job = Assert.Single(snap, j => j.Name == "tick");
        Assert.True(ran >= 2);
        Assert.NotNull(job.LastRunAt);
        Assert.True(job.NextRunAt > job.LastRunAt);
    }

    [Fact]
    public async Task Pause_And_Resume_Work()
    {
        using var pool = new KestrunRunspacePoolManager(1, 1);
        var log = CreateLogger();
        using var svc = new SchedulerService(pool, log, TimeZoneInfo.Utc);

        int ran = 0;
        var interval = TimeSpan.FromMilliseconds(100);
        svc.Schedule("p", interval, async ct => { Interlocked.Increment(ref ran); await Task.CompletedTask; });
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

        int ran = 0;
        svc.Schedule("c", TimeSpan.FromMilliseconds(100), async ct => { Interlocked.Increment(ref ran); await Task.CompletedTask; });
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
}
