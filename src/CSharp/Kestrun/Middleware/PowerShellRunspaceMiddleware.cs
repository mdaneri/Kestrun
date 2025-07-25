using System.Management.Automation;
using Kestrun.Languages;
using Serilog;
using Serilog.Events;

namespace Kestrun.Middleware;
public sealed class PowerShellRunspaceMiddleware(RequestDelegate next, KestrunRunspacePoolManager pool)
{
    private readonly RequestDelegate _next = next ?? throw new ArgumentNullException(nameof(next));
    private readonly KestrunRunspacePoolManager _pool = pool ?? throw new ArgumentNullException(nameof(pool));

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            if (Log.IsEnabled(LogEventLevel.Debug))
                Log.Debug("PowerShellRunspaceMiddleware started for {Path}", context.Request.Path);
            // EnsureRunspacePoolOpen(_pool);
            // Acquire a runspace from the pool and keep it for the whole request
            var runspace = _pool.Acquire();
            using PowerShell ps = PowerShell.Create();
            ps.Runspace = runspace;
            var krRequest = await KestrunRequest.NewRequest(context);
            var krResponse = new KestrunResponse(krRequest);

            // keep a reference for any C# code later in the pipeline
            context.Items[PowerShellDelegateBuilder.KR_REQUEST_KEY] = krRequest;
            context.Items[PowerShellDelegateBuilder.KR_RESPONSE_KEY] = krResponse;
            // Store the PowerShell instance in the context for later use
            context.Items[PowerShellDelegateBuilder.PS_INSTANCE_KEY] = ps;
            Log.Verbose("Setting PowerShell variables for Request and Response in the runspace.");
            // Set the PowerShell variables for the request and response
            var ss = ps.Runspace.SessionStateProxy;
            ss.SetVariable("Context", context);
            ss.SetVariable("Request", krRequest);
            ss.SetVariable("Response", krResponse);

            try
            {
                if (Log.IsEnabled(LogEventLevel.Debug))
                    Log.Debug("PowerShellRunspaceMiddleware - Continuing Pipeline  for {Path}", context.Request.Path);
                await _next(context);                // continue the pipeline
                if (Log.IsEnabled(LogEventLevel.Debug))
                    Log.Debug("PowerShellRunspaceMiddleware completed for {Path}", context.Request.Path);
            }
            finally
            {
                if (ps != null)
                {
                    if (Log.IsEnabled(LogEventLevel.Debug))
                        Log.Debug("Returning runspace to pool: {RunspaceId}", ps.Runspace.InstanceId);
                    _pool.Release(ps.Runspace); // return the runspace to the pool
                    if (Log.IsEnabled(LogEventLevel.Debug))
                        Log.Debug("Disposing PowerShell instance: {InstanceId}", ps.InstanceId);
                    // Dispose the PowerShell instance
                    ps.Dispose();
                    context.Items.Remove(PowerShellDelegateBuilder.PS_INSTANCE_KEY);     // just in case someone re-uses the ctx object                                                             // Dispose() returns the runspace to the pool
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error occurred in PowerShellRunspaceMiddleware");
        }
    }
}