using System.Management.Automation;
using Kestrun.Utilities;
using Serilog;
using Serilog.Events;

namespace Kestrun.Languages;

internal static class PowerShellDelegateBuilder
{
   public const string PS_INSTANCE_KEY = "PS_INSTANCE";
    public const string KR_REQUEST_KEY = "KR_REQUEST";
    public const string KR_RESPONSE_KEY = "KR_RESPONSE";
    internal static RequestDelegate Build(string code)
    {
        if (Log.IsEnabled(LogEventLevel.Debug))
            Log.Debug("Building PowerShell delegate, script length={Length}", code?.Length);

        return async context =>
        {
            if (Log.IsEnabled(LogEventLevel.Debug))
                Log.Debug("PS delegate invoked for {Path}", context.Request.Path);

            if (!context.Items.ContainsKey(PS_INSTANCE_KEY))
            {
                throw new InvalidOperationException("PowerShell runspace not found in context items. Ensure PowerShellRunspaceMiddleware is registered.");
            }
            // Retrieve the PowerShell instance from the context
            Log.Verbose("Retrieving PowerShell instance from context items.");
            PowerShell ps = context.Items[PS_INSTANCE_KEY] as PowerShell
                ?? throw new InvalidOperationException("PowerShell instance not found in context items.");
            if (ps.Runspace == null)
            {
                throw new InvalidOperationException("PowerShell runspace is not set. Ensure PowerShellRunspaceMiddleware is registered.");
            }
            // Ensure the runspace pool is open before executing the script 
            try
            {
                Log.Verbose("Setting PowerShell variables for Request and Response in the runspace.");
                var krRequest = context.Items[KR_REQUEST_KEY] as KestrunRequest
                    ?? throw new InvalidOperationException($"{KR_REQUEST_KEY} key not found in context items.");
                var krResponse = context.Items[KR_RESPONSE_KEY] as KestrunResponse
                    ?? throw new InvalidOperationException($"{KR_RESPONSE_KEY} key not found in context items.");
                ps.AddScript(code);
                // Execute the PowerShell script block
                // Using Task.Run to avoid blocking the thread
                Log.Verbose("Executing PowerShell script...");
                // Using Task.Run to avoid blocking the thread
                // This is necessary to prevent deadlocks in the runspace pool
                // var psResults = await Task.Run(() => ps.Invoke())               // no pool dead-lock
                //     .ConfigureAwait(false);
                //  var psResults = ps.Invoke();
                var psResults = await ps.InvokeAsync().ConfigureAwait(false);
                Log.Verbose($"PowerShell script executed with {psResults.Count} results.");
                if (Log.IsEnabled(LogEventLevel.Debug))
                {
                    Log.Debug("PowerShell script output:");
                    foreach (var result in psResults)
                    {
                        if (result != null)
                        {
                            Log.Debug($"  Result: {result}");
                        }
                    }
                }
                //  var psResults = await Task.Run(() => ps.Invoke());
                // Capture errors and output from the runspace
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
                // If redirect, nothing to return
                if (!string.IsNullOrEmpty(krResponse.RedirectUrl))
                {
                    Log.Verbose($"Redirecting to {krResponse.RedirectUrl}");
                    context.Response.Redirect(krResponse.RedirectUrl);
                    return;
                }
                Log.Verbose("Applying response to HttpResponse...");
                // Apply the response to the HttpResponse

                await krResponse.ApplyTo(context.Response);
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
                Log.Error($"Error processing request: {ex.Message}");
                context.Response.StatusCode = 500; // Internal Server Error
                context.Response.ContentType = "text/plain; charset=utf-8";
                await context.Response.WriteAsync("An error occurred while processing your request.");
            }
            finally
            {
                // CompleteAsync is idempotent – safe to call once more
                try
                {

                    Log.Verbose("Completing response for " + context.Request.Path);
                    await context.Response.CompleteAsync().ConfigureAwait(false);

                }
                catch (ObjectDisposedException odex)
                {
                    // This can happen if the response has already been completed
                    // or the client has disconnected
                    Log.Debug(odex, "Response already completed for {Path}", context.Request.Path);
                }

                catch (InvalidOperationException ioex)
                {
                    // This can happen if the response has already been completed
                    Log.Debug(ioex, "Response already completed for {Path}", context.Request.Path);
                    // No action needed, as the response is already completed
                }
            }
        };
    }
}