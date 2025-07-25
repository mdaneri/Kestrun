using System.Net;
using Kestrun;
using Kestrun.Logging;
using Kestrun.Scheduling;
using Serilog;
using Serilog.Events;
using System.Collections;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Kestrun.Utilities;
using Kestrun.SharedState;

var cwd = Directory.GetCurrentDirectory();

// ───────── 1. Serilog
new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.File("logs/kestrun.log", rollingInterval: RollingInterval.Day)
    .Register("Log", setAsDefault: true);

// ───────── 2. Kestrun host
var host = new KestrunHost("Kestrun+Scheduler", cwd);



// basic Kestrel opts / listener
host.ConfigureListener(
    port: 5000,
    ipAddress: IPAddress.Loopback,
    protocols: Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1
    );

// add PowerShell runtime & global counter
host.AddPowerShellRuntime();
host.AddScheduling(8); // 8 runspaces for the scheduler
// define global variable for visits
SharedStateStore.Set("Visits", new Hashtable { ["Count"] = 0 });
host.EnableConfiguration();
// ── 3.  define SCHEDULED JOBS  ──

// (A) pure C# heartbeat every 10 s

host.Scheduler.Schedule(
    "heartbeat",
    TimeSpan.FromSeconds(10),
    async ct =>
    {
        Log.Information("💓  Heartbeat (C# Native) at {Now:O}", DateTimeOffset.UtcNow);
        await Task.Delay(100, ct);
    },
    runImmediately: true);

// (B) PowerShell inline – every minute
host.Scheduler.Schedule(
    "ps-inline",
    "0 * * * * *",                          // cron: every minute
    System.Management.Automation.ScriptBlock.Create("""
    Write-Information "[$([DateTime]::UtcNow.ToString('o'))] 🌙  Inline PS job ran."
    Write-Information "Runspace Name: $([runspace]::DefaultRunspace.Name)"
    Write-Information "$($Visits['Count']) Visits so far."
    """
));

// (C) PowerShell file – nightly at 03:00
var cleanupFile = new FileInfo(Path.Combine(cwd, "Scripts", "Cleanup.ps1"));
host.Scheduler.Schedule(
    "nightly-clean",
    "0 0 3 * * *",                          // 03:00 daily
    cleanupFile, ScriptLanguage.PowerShell);
// ─────── 4.  ROUTES  ─────────

// increment / show visits (unchanged)
host.AddRoute("/visit", HttpVerb.Get, """
    $Visits["Count"]++
    Write-KrTextResponse "🔢 Visits now: $($Visits['Count'])" 200
""", ScriptLanguage.PowerShell);

// JSON schedule report
host.AddNativeRoute("/schedule/report", HttpVerb.Get, async (req, res) =>
{
    var report = host.Scheduler.GetReport();
    res.WriteJsonResponse(report, 200);
    await Task.Yield();
});

// ───────── 5.  START  ─────────
await host.StartAsync();
Console.WriteLine("Kestrun is running. Hit Ctrl+C to stop.");

// graceful shutdown loop
var done = new ManualResetEventSlim(false);
Console.CancelKeyPress += async (_, e) =>
{
    e.Cancel = true;
    Console.WriteLine("Stopping…");
    await host.StopAsync();
    done.Set();
};
done.Wait();
host.Dispose();
