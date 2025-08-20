using System.Management.Automation;
using Kestrun.Hosting;
using Kestrun.Languages;
using Kestrun.Models;
using Kestrun.Scripting;
using Serilog;
using Serilog.Events;

namespace Kestrun.Middleware;

/// <summary>
/// Initializes a new instance of the <see cref="PowerShellRunspaceMiddleware"/> class.
/// </summary>
/// <param name="next">The next middleware in the pipeline.</param>
/// <param name="pool">The runspace pool manager.</param>
public sealed class PowerShellRunspaceMiddleware(RequestDelegate next, KestrunRunspacePoolManager pool)
{
    private readonly RequestDelegate _next = next ?? throw new ArgumentNullException(nameof(next));
    private readonly KestrunRunspacePoolManager _pool = pool ?? throw new ArgumentNullException(nameof(pool));

    /// <summary>
    /// Processes an HTTP request using a PowerShell runspace from the pool.
    /// </summary>
    /// <param name="context">The HTTP context for the current request.</param>
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            if (Log.IsEnabled(LogEventLevel.Debug))
            {
                Log.Debug("PowerShellRunspaceMiddleware started for {Path}", context.Request.Path);
            }
            // Acquire a runspace from the pool and keep it for the whole request
            var runspace = _pool.Acquire();
            using var ps = PowerShell.Create();
            ps.Runspace = runspace;
            var krRequest = await KestrunRequest.NewRequest(context);
            var krResponse = new KestrunResponse(krRequest);

            // Store the PowerShell instance in the context for later use
            context.Items[PowerShellDelegateBuilder.PS_INSTANCE_KEY] = ps;

            KestrunContext kestrunContext = new(krRequest, krResponse, context);
            // Set the KestrunContext in the HttpContext.Items for later use
            context.Items[PowerShellDelegateBuilder.KR_CONTEXT_KEY] = kestrunContext;

            if (Log.IsEnabled(LogEventLevel.Debug))
            {
                Log.Debug("PowerShellRunspaceMiddleware - Setting KestrunContext in HttpContext.Items for {Path}", context.Request.Path);
            }

            Log.Verbose("Setting PowerShell variables for Request and Response in the runspace.");
            // Set the PowerShell variables for the request and response
            var ss = ps.Runspace.SessionStateProxy;
            ss.SetVariable("Context", kestrunContext);

            try
            {
                if (Log.IsEnabled(LogEventLevel.Debug))
                {
                    Log.Debug("PowerShellRunspaceMiddleware - Continuing Pipeline  for {Path}", context.Request.Path);
                }

                await _next(context);                // continue the pipeline
                if (Log.IsEnabled(LogEventLevel.Debug))
                {
                    Log.Debug("PowerShellRunspaceMiddleware completed for {Path}", context.Request.Path);
                }
            }
            finally
            {
                if (ps != null)
                {
                    if (Log.IsEnabled(LogEventLevel.Debug))
                    {
                        Log.Debug("Returning runspace to pool: {RunspaceId}", ps.Runspace.InstanceId);
                    }

                    _pool.Release(ps.Runspace); // return the runspace to the pool
                    if (Log.IsEnabled(LogEventLevel.Debug))
                    {
                        Log.Debug("Disposing PowerShell instance: {InstanceId}", ps.InstanceId);
                    }
                    // Dispose the PowerShell instance
                    ps.Dispose();
                    _ = context.Items.Remove(PowerShellDelegateBuilder.PS_INSTANCE_KEY);     // just in case someone re-uses the ctx object                                                             // Dispose() returns the runspace to the pool
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error occurred in PowerShellRunspaceMiddleware");
        }
    }
}