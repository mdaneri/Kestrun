using System.Reflection;
using Kestrun.Hosting;
using Kestrun.Languages;
using Kestrun.Logging;
using Kestrun.Models;
using Kestrun.SharedState;
using Microsoft.CodeAnalysis.CSharp;
using Serilog.Events;

internal static class DelegateBuilder
{
    /// <summary>
    /// Prepares the Kestrun context, response, and script globals for execution.
    /// Encapsulates request parsing, shared state snapshot, arg injection, and logging.
    /// </summary>
    /// <param name="ctx">The current HTTP context.</param>
    /// <param name="log">Logger for diagnostics.</param>
    /// <param name="args">Optional variables to inject into the globals.</param>
    /// <returns>Tuple containing the prepared CsGlobals, KestrunResponse, and KestrunContext.</returns>
    internal static async Task<(CsGlobals Globals, KestrunResponse Response, KestrunContext Context)> PrepareExecutionAsync(
        HttpContext ctx,
        Serilog.ILogger log,
        Dictionary<string, object?>? args)
    {
        var krRequest = await KestrunRequest.NewRequest(ctx).ConfigureAwait(false);
        var krResponse = new KestrunResponse(krRequest);
        var Context = new KestrunContext(krRequest, krResponse, ctx);
        if (log.IsEnabled(LogEventLevel.Debug))
        {
            log.DebugSanitized("Kestrun context created for {Path}", ctx.Request.Path);
        }

        // Create a shared state dictionary that will be used to store global variables
        // This will be shared across all requests and can be used to store state
        // that needs to persist across multiple requests
        if (log.IsEnabled(LogEventLevel.Debug))
        {
            log.Debug("Creating shared state store for Kestrun context");
        }

        var glob = new Dictionary<string, object?>(SharedStateStore.Snapshot());
        if (log.IsEnabled(LogEventLevel.Debug))
        {
            log.Debug("Shared state store created with {Count} items", glob.Count);
        }

        // Inject the provided arguments into the globals so the script can access them
        if (args != null && args.Count > 0)
        {
            if (log.IsEnabled(LogEventLevel.Debug))
            {
                log.Debug("Setting variables from arguments: {Count}", args.Count);
            }

            foreach (var kv in args)
            {
                glob[kv.Key] = kv.Value; // add args to globals
            }
        }

        // Create a new CsGlobals instance with the current context and shared state
        var globals = new CsGlobals(glob, Context);
        return (globals, krResponse, Context);
    }


    /// <summary>
    /// Decides the VB return type string that matches TResult.
    /// </summary>
    /// <param name="ctx">The current HTTP context.</param>
    /// <param name="response">The Kestrun response to apply.</param>
    /// <param name="log">The logger to use for logging.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    internal static async Task ApplyResponseAsync(HttpContext ctx, KestrunResponse response, Serilog.ILogger log)
    {
        if (log.IsEnabled(LogEventLevel.Debug))
        {
            log.DebugSanitized("Applying response to Kestrun context for {Path}", ctx.Request.Path);
        }

        if (!string.IsNullOrEmpty(response.RedirectUrl))
        {
            ctx.Response.Redirect(response.RedirectUrl);
            return;
        }

        await response.ApplyTo(ctx.Response).ConfigureAwait(false);

        if (log.IsEnabled(LogEventLevel.Debug))
        {
            log.DebugSanitized("Response applied to Kestrun context for {Path}", ctx.Request.Path);
        }
    }
}
