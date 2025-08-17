using System.Management.Automation;
using Kestrun.Hosting;
using Kestrun.Models;
using Kestrun.Utilities;
using Serilog;
using Serilog.Events;

namespace Kestrun.Languages;

internal static class PowerShellDelegateBuilder
{
    public const string PS_INSTANCE_KEY = "PS_INSTANCE"; 
    public const string KR_CONTEXT_KEY = "KR_CONTEXT";

    internal static RequestDelegate Build(string code, Serilog.ILogger log, Dictionary<string, object?>? arguments)
    {
        if (log.IsEnabled(LogEventLevel.Debug))
            log.Debug("Building PowerShell delegate, script length={Length}", code?.Length);

        return async context =>
        {
            if (log.IsEnabled(LogEventLevel.Debug))
                log.Debug("PS delegate invoked for {Path}", context.Request.Path);

            if (!context.Items.ContainsKey(PS_INSTANCE_KEY))
            {
                throw new InvalidOperationException("PowerShell runspace not found in context items. Ensure PowerShellRunspaceMiddleware is registered.");
            }
            // Retrieve the PowerShell instance from the context
            log.Verbose("Retrieving PowerShell instance from context items.");
            PowerShell ps = context.Items[PS_INSTANCE_KEY] as PowerShell
                ?? throw new InvalidOperationException("PowerShell instance not found in context items.");
            if (ps.Runspace == null)
            {
                throw new InvalidOperationException("PowerShell runspace is not set. Ensure PowerShellRunspaceMiddleware is registered.");
            }
            // Ensure the runspace pool is open before executing the script 
            try
            {
                if (arguments != null && arguments.Count > 0)
                {
                    log.Verbose("Setting PowerShell variables from arguments: {Count}", arguments.Count);
                    // Set the arguments as PowerShell variables in the runspace
                    var ss = ps.Runspace.SessionStateProxy;
                    foreach (var arg in arguments)
                    {
                        ss.SetVariable(arg.Key, arg.Value);
                    }
                }
                log.Verbose("Setting PowerShell variables for Request and Response in the runspace.");
                var krContext = context.Items[KR_CONTEXT_KEY] as KestrunContext
                    ?? throw new InvalidOperationException($"{KR_CONTEXT_KEY} key not found in context items.");
                ps.AddScript(code);
                // Execute the PowerShell script block 
                log.Verbose("Executing PowerShell script...");

                var psResults = await ps.InvokeAsync().ConfigureAwait(false);
                log.Verbose($"PowerShell script executed with {psResults.Count} results.");
                if (log.IsEnabled(LogEventLevel.Debug))
                {
                    log.Debug("PowerShell script output:");
                    foreach (var r in psResults.Take(10))      // first 10 only
                        log.Debug("   • {Result}", r);
                    if (psResults.Count > 10)
                        log.Debug("   … {Count} more", psResults.Count - 10);
                }
                if (ps.HadErrors || ps.Streams.Error.Count != 0)
                {
                    await BuildError.ResponseAsync(context, ps);
                    return;
                }
                else if (ps.Streams.Verbose.Count > 0 || ps.Streams.Debug.Count > 0 || ps.Streams.Warning.Count > 0 || ps.Streams.Information.Count > 0)
                {
                    log.Verbose("PowerShell script completed with verbose/debug/warning/info messages.");
                    log.Verbose(BuildError.Text(ps));
                }

                log.Verbose("PowerShell script completed successfully.");
                // If redirect, nothing to return
                if (!string.IsNullOrEmpty(krContext.Response.RedirectUrl))
                {
                    log.Verbose($"Redirecting to {krContext.Response.RedirectUrl}");
                    context.Response.Redirect(krContext.Response.RedirectUrl);
                    return;
                }
                log.Verbose("Applying response to HttpResponse...");
                // Apply the response to the HttpResponse

                await krContext.Response.ApplyTo(context.Response);
                return;
            }
            // optional: catch client cancellation to avoid noisy logs
            catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
            {
                // client disconnected – nothing to send
            }
            catch (Exception ex)
            {
                // Log the exception (optional)
                log.Error(ex, "PowerShell script failed - {Preview}", code?[..Math.Min(40, code.Length)]);
                context.Response.StatusCode = 500; // Internal Server Error
                context.Response.ContentType = "text/plain; charset=utf-8";
                await context.Response.WriteAsync("An error occurred while processing your request.");
            }
            finally
            {
                // CompleteAsync is idempotent – safe to call once more
                try
                {

                    log.Verbose("Completing response for " + context.Request.Path);
                    await context.Response.CompleteAsync().ConfigureAwait(false);
                }
                catch (ObjectDisposedException odex)
                {
                    // This can happen if the response has already been completed
                    // or the client has disconnected
                    log.Debug(odex, "Response already completed for {Path}", context.Request.Path);
                }

                catch (InvalidOperationException ioex)
                {
                    // This can happen if the response has already been completed
                    log.Debug(ioex, "Response already completed for {Path}", context.Request.Path);
                    // No action needed, as the response is already completed
                }
            }
        };
    }
}