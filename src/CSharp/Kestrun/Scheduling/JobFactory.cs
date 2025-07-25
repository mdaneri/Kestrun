using System.Management.Automation;
using Kestrun;
using Kestrun.Utilities;
using Serilog;
using Serilog.Events;

namespace Kestrun.Scheduling;

internal static class JobFactory
{
    public static Func<CancellationToken, Task> Create(
        ScriptLanguage lang,
        string code,
        KestrunRunspacePoolManager pool,
        Serilog.ILogger log)
    {
        return lang switch
        {
            ScriptLanguage.PowerShell => PowerShellJob(pool, code, log),
            ScriptLanguage.CSharp => RoslynJob(code),
            _ => throw new NotSupportedException($"Language {lang} not supported.")
        };
    }

    public static async Task<Func<CancellationToken, Task>> CreateAsync(
       ScriptLanguage lang,
       FileInfo fileInfo,
       KestrunRunspacePoolManager pool,
       Serilog.ILogger log, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(fileInfo);
        if (!fileInfo.Exists)
            throw new FileNotFoundException(fileInfo.FullName);
        string code = await File.ReadAllTextAsync(fileInfo.FullName, ct);
        return Create(lang, code, pool, log);
    }

    public static Func<CancellationToken, Task> Create(
          ScriptLanguage lang,
          FileInfo fileInfo,
          KestrunRunspacePoolManager pool,
          Serilog.ILogger log, CancellationToken ct = default)
    {
        return CreateAsync(lang, fileInfo, pool, log, ct).GetAwaiter().GetResult();
    }


    /* ----------------  PowerShell  ---------------- */
    private static Func<CancellationToken, Task> PowerShellJob(
        KestrunRunspacePoolManager pool,
        string code,
         Serilog.ILogger log)
    {
        return async ct =>
        {
            var runspace = pool.Acquire();
            try
            {
                using PowerShell ps = PowerShell.Create();
                ps.Runspace = runspace;
                ps.AddScript(code);

                //    var psResults = await ps.InvokeAsync().ConfigureAwait(false);
                using var reg = ct.Register(() => ps.Stop());

                var psResults = await ps.InvokeAsync().WaitAsync(ct).ConfigureAwait(false);

                log.Verbose($"PowerShell script executed with {psResults.Count} results.");
                if (log.IsEnabled(LogEventLevel.Debug))
                {
                    log.Debug("PowerShell script output:");
                    foreach (var r in psResults.Take(10))      // first 10 only
                        log.Debug("   • {Result}", r);
                    if (psResults.Count > 10)
                        log.Debug("   … {Count} more", psResults.Count - 10);
                }

                if (ps.HadErrors || ps.Streams.Error.Count != 0 || ps.Streams.Verbose.Count > 0 || ps.Streams.Debug.Count > 0 || ps.Streams.Warning.Count > 0 || ps.Streams.Information.Count > 0)
                {
                    log.Verbose("PowerShell script completed with verbose/debug/warning/info messages.");
                    log.Verbose(BuildError.Text(ps));
                }
            }
            catch (Exception ex)
            {
                log.Error(ex, "[Scheduler] PowerShell job failed – {Preview}", code[..Math.Min(40, code.Length)]);
                throw;
            }
            finally
            {
                // Ensure we release the runspace back to the pool                 
                pool.Release(runspace);

            }
        };
    }

    /* ----------------  C# (Roslyn) ---------------- */
    private static Func<CancellationToken, Task> RoslynJob(string code)
    {
        return RoslynJobFactory.Build(code);   // from previous answer
    }
}
