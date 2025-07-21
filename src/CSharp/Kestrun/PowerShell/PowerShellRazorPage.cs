// Kestrun -– PowerShell-backed Razor Pages
// -----------------------------------------------------------------------------
// Middleware that lets any Razor view (*.cshtml) load a sibling PowerShell
// script (*.cshtml.ps1) in the SAME request.  The script can set `$Model` which
// then becomes available to the Razor page through HttpContext.Items.
// -----------------------------------------------------------------------------
//
// Usage (inside KestrunHost.ApplyConfiguration):
//     builder.Services.AddRazorPages();                    // already present
//     …
/*  AFTER you build App and create _runspacePool:  */
//
//     App.UsePowerShellRazorPages(_runspacePool);
//
// That’s it – no per-page registration.
//

using System;
using System.IO;
using System.Management.Automation;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;

namespace Kestrun;

public static class PowerShellRazorPage
{
    private const string MODEL_KEY = "PageModel";

    /// <summary>
    /// Enables <c>.cshtml</c> + <c>.cshtml.ps1</c> pairs.
    /// For a request to <c>/Foo</c> it will, in order:
    /// <list type="number">
    ///   <item><description>Look for <c>Pages/Foo.cshtml</c></description></item>
    ///   <item><description>If a <c>Pages/Foo.cshtml.ps1</c> exists, execute it
    ///       in the supplied runspace-pool</description></item>
    ///   <item><description>Whatever the script assigns to <c>$Model</c>
    ///       is copied to <c>HttpContext.Items["PageModel"]</c></description></item>
    /// </list>
    /// Razor pages (or a generic <see cref="PowerShellPageModel"/>) can then
    /// read that dynamic object.
    /// </summary>
    /// <param name="app">The <see cref="WebApplication"/> pipeline.</param>
    /// <param name="pool">Kestrun’s shared <see cref="KestrunRunspacePoolManager"/>.</param>
    /// <returns><paramref name="app"/> for fluent chaining.</returns>
    public static WebApplication UsePowerShellRazorPages(
        this WebApplication app,
        KestrunRunspacePoolManager pool)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(pool);

        if (Log.IsEnabled(LogEventLevel.Debug))
            Log.Debug("Configuring PowerShell Razor Pages middleware");

        var pagesRoot = Path.Combine(app.Environment.ContentRootPath, "Pages");
        Log.Information("Using Pages directory: {Path}", pagesRoot);
        if (!Directory.Exists(pagesRoot))
        {
            Log.Warning("Pages directory not found: {Path}", pagesRoot);
         //  throw new DirectoryNotFoundException($"Pages directory not found: {pagesRoot}");
        }

        // MUST run before MapRazorPages()
        app.Use(async (context, next) =>
        {
            var relPath = context.Request.Path.Value?.Trim('/');
            if (string.IsNullOrEmpty(relPath))
            {
                await next();
                return;
            }

            var view = Path.Combine(pagesRoot, relPath + ".cshtml");
            var psfile = view + ".ps1";

            if (!File.Exists(view) || !File.Exists(psfile))
            {
                await next();
                return;
            }

            try
            {
                using var ps = PowerShell.Create(pool.Acquire());
                var ss = ps.Runspace.SessionStateProxy;
                ss.SetVariable("Context", context);
                ss.SetVariable("Model", null); // ensure it’s null before script runs
               // ps.AddScript(await File.ReadAllTextAsync(view, context.RequestAborted));
                 ps.AddScript(await File.ReadAllTextAsync(psfile, context.RequestAborted));

                await ps.InvokeAsync().ConfigureAwait(false);

                var model = ps.Runspace.SessionStateProxy.GetVariable("Model");
                if (model is not null)
                    context.Items[MODEL_KEY] = model;

                if (ps.HadErrors || ps.Streams.Error.Count != 0)
                {
                    await BuildError.ResponseAsync(context, ps);
                    return;
                }
                else if (ps.Streams.Verbose.Count > 0 || ps.Streams.Debug.Count > 0 || ps.Streams.Warning.Count > 0 || ps.Streams.Information.Count > 0)
                {
                    Log.Verbose("PowerShell script completed with verbose/debug/warning/info messages.");
                    Log.Verbose(BuildError.Text(ps));
                }
                Log.Verbose("PowerShell script completed successfully.");
                try
                {
                    await next();                // continue the pipeline
                    if (Log.IsEnabled(LogEventLevel.Debug))
                        Log.Debug("PowerShell Razor Page completed for {Path}", context.Request.Path);
                }
                finally
                {
                    if (ps != null)
                    {
                        if (Log.IsEnabled(LogEventLevel.Debug))
                            Log.Debug("Returning runspace to pool: {RunspaceId}", ps.Runspace.InstanceId);
                        pool.Release(ps.Runspace); // return the runspace to the pool
                        if (Log.IsEnabled(LogEventLevel.Debug))
                            Log.Debug("Disposing PowerShell instance: {InstanceId}", ps.InstanceId);
                        // Dispose the PowerShell instance
                        ps.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error occurred in PowerShell Razor Page middleware for {Path}", context.Request.Path);
            }
        });

        // static files & routing can be added earlier in pipeline
        app.MapRazorPages();
        return app;
    }
}
