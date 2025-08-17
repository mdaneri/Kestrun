using Kestrun;
using Kestrun.Scheduling;
using Kestrun.Scripting;
using Serilog;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace KestrunTests.Scheduling;
public class SchedulerServiceTests
{
    [Fact]
    public async Task Schedule_PowerShellJob_Executes()
    {
        var logger = new LoggerConfiguration().MinimumLevel.Error().CreateLogger();
        using var pool = new KestrunRunspacePoolManager(1, 1);
        using var scheduler = new SchedulerService(pool, logger);
        scheduler.Schedule("ps", TimeSpan.FromMilliseconds(10), "1+1", ScriptLanguage.PowerShell, runImmediately: true);
        await Task.Delay(2000);
        var info = scheduler.GetSnapshot().Single(j => j.Name == "ps");
        Assert.NotNull(info.LastRunAt);
    }
}