using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Python.Runtime;
using Microsoft.ClearScript.V8;
using System;
using System.IO;
using System.Collections.Concurrent;
using System.Collections;
using System.Collections.Immutable;
using System.Text.Json;
using System.Text;
using System.Net;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Kestrun;
using Microsoft.CodeAnalysis;
using System.Reflection;
using Microsoft.CodeAnalysis.CSharp;
using System.Security.Cryptography.X509Certificates;
using System.Security;
using Microsoft.AspNetCore.Hosting;
using Serilog;
using Serilog.Events;
using Microsoft.AspNetCore.StaticFiles.Infrastructure;
using Microsoft.AspNetCore.ResponseCompression;
using System.Collections.Specialized;
using Microsoft.AspNetCore.Mvc;               // MvcOptions, IMvcBuilder
using Microsoft.AspNetCore.Mvc.RazorPages;    // RazorPagesOptions

using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Antiforgery;     // extension methods
//using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Mvc.Razor.RuntimeCompilation;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Kestrun.Scheduling;

namespace Kestrun;

public class KestrunHost
{
    #region Fields

    private const string PS_INSTANCE_KEY = "PS_INSTANCE";
    private const string KR_REQUEST_KEY = "KR_REQUEST";
    private const string KR_RESPONSE_KEY = "KR_RESPONSE";
    // Shared state across routes
    private readonly ConcurrentDictionary<string, string> sharedState = new();
    private readonly WebApplicationBuilder builder;
    private WebApplication? App;

    public KestrunOptions Options { get; private set; }
    private readonly List<string> _modulePaths = [];

    private bool _isConfigured = false;

    private KestrunRunspacePoolManager? _runspacePool;
    public string? KestrunRoot { get; private set; }

    public SharedStateStore SharedState { get; } = new();

    public SchedulerService? Scheduler { get; private set; }

    // ── ✦ QUEUE #1 : SERVICE REGISTRATION ✦ ─────────────────────────────
    private readonly List<Action<IServiceCollection>> _serviceQueue = [];

    // ── ✦ QUEUE #2 : MIDDLEWARE STAGES ✦ ────────────────────────────────
    private readonly List<Action<IApplicationBuilder>> _middlewareQueue = [];


    #endregion


    // Accepts optional module paths (from PowerShell)
    #region Constructor

    public KestrunHost(string? appName, string? kestrunRoot = null, string[]? modulePathsObj = null) :
            this(appName, Log.Logger, kestrunRoot, modulePathsObj)
    { }
    public KestrunHost(string? appName, bool sampleLogger, string? kestrunRoot = null, string[]? modulePathsObj = null) :
               this(appName, sampleLogger ? CreateDefaultLogger() : Log.Logger, kestrunRoot, modulePathsObj)
    { }
    public KestrunHost(string? appName, Serilog.ILogger logger, string? kestrunRoot = null, string[]? modulePathsObj = null)
    {
        // Initialize Serilog logger if not provided


        if (Log.IsEnabled(LogEventLevel.Debug))
            Log.Debug("KestrunHost constructor called with appName: {AppName}, default logger: {DefaultLogger}, kestrunRoot: {KestrunRoot}, modulePathsObj length: {ModulePathsLength}", appName, logger == null, KestrunRoot, modulePathsObj?.Length ?? 0);
        if (!string.IsNullOrWhiteSpace(kestrunRoot))
        {
            Log.Information("Setting Kestrun root directory: {KestrunRoot}", kestrunRoot);
            Directory.SetCurrentDirectory(kestrunRoot);
            KestrunRoot = kestrunRoot;
        }
        var kestrunModulePath = string.Empty;
        if (modulePathsObj is null || (modulePathsObj?.Any(p => p.Contains("Kestrun.psm1", StringComparison.Ordinal)) == false))
        {
            kestrunModulePath = PowerShellModuleLocator.LocateKestrunModule();
            if (string.IsNullOrWhiteSpace(kestrunModulePath))
            {
                Log.Fatal("Kestrun module not found. Ensure the Kestrun module is installed.");
                throw new FileNotFoundException("Kestrun module not found.");
            }

            Log.Information("Found Kestrun module at: {KestrunModulePath}", kestrunModulePath);
            Log.Verbose("Adding Kestrun module path: {KestrunModulePath}", kestrunModulePath);
            _modulePaths.Add(kestrunModulePath);
        }


        builder = WebApplication.CreateBuilder();
        // Add Serilog to ASP.NET Core logging
        builder.Host.UseSerilog();
        if (string.IsNullOrEmpty(appName))
        {
            Log.Information("No application name provided, using default.");
            Options = new KestrunOptions();
        }
        else
        {
            Log.Information("Setting application name: {AppName}", appName);
            Options = new KestrunOptions { ApplicationName = appName };
        }

        // Store module paths if provided
        if (modulePathsObj is IEnumerable<object> modulePathsEnum)
        {
            foreach (var modPathObj in modulePathsEnum)
            {
                if (modPathObj is string modPath && !string.IsNullOrWhiteSpace(modPath))
                {
                    if (File.Exists(modPath))
                    {
                        Log.Information("[KestrunHost] Adding module path: {ModPath}", modPath);
                        _modulePaths.Add(modPath);
                    }
                    else
                    {
                        Log.Warning("[KestrunHost] Module path does not exist: {ModPath}", modPath);
                    }
                }
                else
                {
                    Log.Warning("[KestrunHost] Invalid module path provided.");
                }
            }
        }

        Log.Information("Current working directory: {CurrentDirectory}", Directory.GetCurrentDirectory());
    }
    #endregion

    #region Helpers


    /// <summary>
    /// Creates a default Serilog logger with basic configuration.
    /// </summary>
    /// <returns>The configured Serilog logger.</returns>
    private static Serilog.ILogger CreateDefaultLogger()
    {
        Log.Logger = new LoggerConfiguration()
         .MinimumLevel.Debug()
         .Enrich.FromLogContext()
         .WriteTo.File("logs/kestrun.log", rollingInterval: RollingInterval.Day)
         .CreateLogger();
        return Log.Logger;
    }
    #endregion

    #region Services
    private void KestrelServices(WebApplicationBuilder builder)
    {
        if (Log.IsEnabled(LogEventLevel.Debug))
            Log.Debug("Configuring Kestrel services");
        builder = builder ?? throw new ArgumentNullException(nameof(builder));

        // Disable Kestrel's built-in console lifetime management
        builder.Services.AddSingleton<IHostLifetime, NoopHostLifetime>();
        try
        {
            builder.Services.AddResponseCompression(options =>
            {
                options.EnableForHttps = true;
            });
            builder.Services.AddRazorPages();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to configure response compression.", ex);
        }
    }

    #endregion

    #region ListenerOptions 

    public void ConfigureListener(int port, IPAddress? ipAddress = null, X509Certificate2? x509Certificate = null, HttpProtocols protocols = HttpProtocols.Http1, bool useConnectionLogging = false)
    {
        if (Log.IsEnabled(LogEventLevel.Debug))
            Log.Debug("ConfigureListener port={Port}, ipAddress={IPAddress}, protocols={Protocols}, useConnectionLogging={UseConnectionLogging}, certificate supplied={HasCert}", port, ipAddress, protocols, useConnectionLogging, x509Certificate != null);


        Options.Listeners.Add(new ListenerOptions
        {
            IPAddress = ipAddress ?? IPAddress.Any,
            Port = port,
            UseHttps = x509Certificate != null,
            X509Certificate = x509Certificate,
            Protocols = protocols,
            UseConnectionLogging = useConnectionLogging
        });

    }

    public void ConfigureListener(int port, IPAddress? ipAddress = null, bool useConnectionLogging = false)
    {
        ConfigureListener(port: port, ipAddress: ipAddress, x509Certificate: null, protocols: HttpProtocols.Http1, useConnectionLogging: useConnectionLogging);
    }

    #endregion


    private RequestDelegate BuildJsDelegate(string code)
    {
        if (Log.IsEnabled(LogEventLevel.Debug))
            Log.Debug("Building JavaScript delegate, script length={Length}", code?.Length);
        var engine = new V8ScriptEngine();
        engine.AddHostType("KestrunResponse", typeof(KestrunResponse));
        engine.Execute(code);               // script defines global  function handle(ctx, res) { ... }

        return async context =>
        {
            if (Log.IsEnabled(LogEventLevel.Debug))
                Log.Debug("JS delegate invoked for {Path}", context.Request.Path);

            var krRequest = await KestrunRequest.NewRequest(context);
            var krResponse = new KestrunResponse(krRequest);
            engine.Script.handle(context, krResponse);

            if (!string.IsNullOrEmpty(krResponse.RedirectUrl))
                return;

            await krResponse.ApplyTo(context.Response);
        };
    }



    #region Python


    static public void ConfigurePythonRuntimePath(string path)
    {
        Python.Runtime.Runtime.PythonDLL = path;
    }
    // ---------------------------------------------------------------------------
    //  helpers at class level
    // ---------------------------------------------------------------------------
    private static readonly object _pyGate = new();
    private static bool _pyInit = false;

    private static void EnsurePythonEngine()
    {
        if (_pyInit) return;

        lock (_pyGate)
        {
            if (_pyInit) return;          // double-check

            // If you need a specific DLL, set Runtime.PythonDLL
            // or expose it via the PYTHONNET_PYDLL environment variable.
            // Runtime.PythonDLL = @"C:\Python312\python312.dll";

            PythonEngine.Initialize();        // load CPython once
            PythonEngine.BeginAllowThreads(); // let other threads run
            _pyInit = true;
        }
    }

    // ---------------------------------------------------------------------------
    //  per-route delegate builder
    // ---------------------------------------------------------------------------
    private RequestDelegate BuildPyDelegate(string code)
    {
        if (Log.IsEnabled(LogEventLevel.Debug))
            Log.Debug("Building Python delegate, script length={Length}", code?.Length);
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Python script code cannot be empty.", nameof(code));
        EnsurePythonEngine();                 // one-time init

        // ---------- compile the script once ----------
        using var gil = Py.GIL();           // we are on the caller's thread
        using var scope = Py.CreateScope();

        /*  Expect the user script to contain:

                def handle(ctx, res):
                    # ctx -> ASP.NET HttpContext (proxied)
                    # res -> KestrunResponse    (proxied)
                    ...

            Scope.Exec compiles & executes that code once per route.
        */
        scope.Exec(code);
        dynamic pyHandle = scope.Get("handle");

        // ---------- return a RequestDelegate ----------
        return async context =>
        {
            if (Log.IsEnabled(LogEventLevel.Debug))
                Log.Debug("Python delegate invoked for {Path}", context.Request.Path);

            try
            {
                using var _ = Py.GIL();       // enter GIL for *this* request
                var krRequest = await KestrunRequest.NewRequest(context);
                var krResponse = new KestrunResponse(krRequest);

                // Call the Python handler (Python → .NET marshal is automatic)
                pyHandle(context, krResponse, context);

                // redirect?
                if (!string.IsNullOrEmpty(krResponse.RedirectUrl))
                {
                    context.Response.Redirect(krResponse.RedirectUrl);
                    return;                   // finally-block will CompleteAsync
                }

                // normal response
                await krResponse.ApplyTo(context.Response).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // optional logging
                Log.Error($"Python route error: {ex}");
                context.Response.StatusCode = 500;
                context.Response.ContentType = "text/plain; charset=utf-8";
                await context.Response.WriteAsync(
                    "Python script failed while processing the request.").ConfigureAwait(false);
            }
            finally
            {
                // Always flush & close so the client doesn’t hang
                try { await context.Response.CompleteAsync().ConfigureAwait(false); }
                catch (ObjectDisposedException) { /* client disconnected */ }
            }
        };
    }



    #endregion

    #region F#
    private RequestDelegate BuildFsDelegate(string code)
    { // F# scripting not implemented yet
        if (Log.IsEnabled(LogEventLevel.Debug))
            Log.Debug("Building F# delegate, script length={Length}", code?.Length);
        throw new NotImplementedException("F# scripting is not yet supported in Kestrun.");
    }
    #endregion

    #region C#

    // ---------------------------------------------------------------------------
    //  C# delegate builder  –  now takes optional imports / references
    // ---------------------------------------------------------------------------
    public record CsGlobals(KestrunRequest Request, KestrunResponse Response, HttpContext Context, IReadOnlyDictionary<string, object?> Globals);
    private RequestDelegate BuildCsDelegate(
            string code, string[]? extraImports,
            Assembly[]? extraRefs, LanguageVersion languageVersion = LanguageVersion.CSharp12)
    {
        if (Log.IsEnabled(LogEventLevel.Debug))
            Log.Debug("Building C# delegate, script length={Length}, imports={ImportsCount}, refs={RefsCount}, lang={Lang}",
                code?.Length, extraImports?.Length ?? 0, extraRefs?.Length ?? 0, languageVersion);

        // 1. Compose ScriptOptions
        var opts = ScriptOptions.Default
                   .WithImports("System", "System.Linq", "System.Threading.Tasks", "Microsoft.AspNetCore.Http")
                   .WithReferences(typeof(HttpContext).Assembly, typeof(KestrunResponse).Assembly)
                   .WithLanguageVersion(languageVersion);
        extraImports ??= ["Kestrun"];
        if (!extraImports.Contains("Kestrun"))
        {
            var importsList = extraImports.ToList();
            importsList.Add("Kestrun");
            extraImports = [.. importsList];
        }
        if (extraImports is { Length: > 0 })
            opts = opts.WithImports(opts.Imports.Concat(extraImports));

        if (extraRefs is { Length: > 0 })
            opts = opts.WithReferences(opts.MetadataReferences
                                          .Concat(extraRefs.Select(r => MetadataReference.CreateFromFile(r.Location))));

        // 1. Inject each global as a top-level script variable
        var allGlobals = SharedState.Snapshot();
        if (allGlobals.Count > 0)
        {
            var sb = new StringBuilder();
            foreach (var kvp in allGlobals)
            {
                sb.AppendLine(
                  $"var {kvp.Key} = ({kvp.Value?.GetType().FullName ?? "object"})Globals[\"{kvp.Key}\"];");
            }
            code = sb + code;
        }
        // 2. Compile once
        var script = CSharpScript.Create(code, opts, typeof(CsGlobals));
        var diagnostics = script.Compile();

        // Check for compilation errors
        if (diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error))
            throw new CompilationErrorException("C# route code compilation failed", diagnostics);

        // Log warnings if any (optional - for debugging)
        var warnings = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Warning).ToArray();
        if (warnings.Length != 0)
        {
            Log.Warning($"C# script compilation completed with {warnings.Length} warning(s):");
            foreach (var warning in warnings)
            {
                var location = warning.Location.IsInSource
                    ? $" at line {warning.Location.GetLineSpan().StartLinePosition.Line + 1}"
                    : "";
                Log.Warning($"  Warning [{warning.Id}]: {warning.GetMessage()}{location}");
            }
        }

        // 3. Build the per-request delegate
        return async context =>
        {
            try
            {
                var krRequest = await KestrunRequest.NewRequest(context);
                var krResponse = new KestrunResponse(krRequest);
                await script.RunAsync(new CsGlobals(krRequest, krResponse, context, allGlobals)).ConfigureAwait(false);

                if (!string.IsNullOrEmpty(krResponse.RedirectUrl))
                {
                    context.Response.Redirect(krResponse.RedirectUrl);
                    return;
                }

                await krResponse.ApplyTo(context.Response).ConfigureAwait(false);
            }
            finally
            {
                await context.Response.CompleteAsync().ConfigureAwait(false);
            }
        };
    }
    #endregion

    #region PowerShell



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
                context.Items[KR_REQUEST_KEY] = krRequest;
                context.Items[KR_RESPONSE_KEY] = krResponse;
                // Store the PowerShell instance in the context for later use
                context.Items[PS_INSTANCE_KEY] = ps;
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
                        context.Items.Remove(PS_INSTANCE_KEY);     // just in case someone re-uses the ctx object                                                             // Dispose() returns the runspace to the pool
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error occurred in PowerShellRunspaceMiddleware");
            }
        }
    }


    private RequestDelegate BuildPsDelegate(string code)
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
    #endregion


    #region Route
    public delegate Task KestrunHandler(KestrunRequest req, KestrunResponse res);

    public void AddNativeRoute(string pattern, HttpVerb httpVerb, KestrunHandler handler)
    {
        if (Log.IsEnabled(LogEventLevel.Debug))
            Log.Debug("AddNativeRoute called with pattern={Pattern}, httpVerb={HttpVerb}", pattern, httpVerb);
        AddNativeRoute(pattern: pattern, httpVerbs: [httpVerb], handler: handler);
    }

    public void AddNativeRoute(string pattern, IEnumerable<HttpVerb> httpVerbs, KestrunHandler handler)
    {
        if (Log.IsEnabled(LogEventLevel.Debug))
            Log.Debug("AddNativeRoute called with pattern={Pattern}, httpVerbs={HttpVerbs}", pattern, string.Join(", ", httpVerbs));
        if (App is null)
            throw new InvalidOperationException("WebApplication is not initialized. Call EnableConfiguration first.");
        string[] methods = [.. httpVerbs.Select(v => v.ToMethodString())];
        App.MapMethods(pattern, methods, async context =>
        {
            var req = await KestrunRequest.NewRequest(context);
            var res = new KestrunResponse(req);
            await handler(req, res);
            await res.ApplyTo(context.Response);
        });
    }

    public void AddRoute(string pattern,
                                     HttpVerb httpVerbs,
                                       string scriptBlock,
                                       ScriptLanguage language = ScriptLanguage.PowerShell,
                                       string[]? extraImports = null,
                                       Assembly[]? extraRefs = null)
    {
        AddRoute(pattern, [httpVerbs], scriptBlock, language, extraImports, extraRefs);
    }

    public void AddRoute(string pattern,
                                IEnumerable<HttpVerb> httpVerbs,
                                string scriptBlock,
                                ScriptLanguage language = ScriptLanguage.PowerShell,
                                string[]? extraImports = null,
                                Assembly[]? extraRefs = null)
    {
        if (Log.IsEnabled(LogEventLevel.Debug))
            Log.Debug("AddRoute called with pattern={Pattern}, language={Language}, method={Methods}", pattern, language, httpVerbs);
        if (App is null)
            throw new InvalidOperationException(
                "WebApplication is not initialized. Call EnableConfiguration first.");
        if (string.IsNullOrWhiteSpace(scriptBlock))
            throw new ArgumentException("Script block cannot be empty.", nameof(scriptBlock));

        if (string.IsNullOrWhiteSpace(pattern))
            throw new ArgumentException("Route pattern cannot be empty.", nameof(pattern));
        if (httpVerbs.Count() == 0) httpVerbs = [HttpVerb.Get];
        try
        {
            // compile once – return an HttpContext->Task delegate
            var handler = language switch
            {
                ScriptLanguage.PowerShell => BuildPsDelegate(scriptBlock),
                ScriptLanguage.CSharp => BuildCsDelegate(scriptBlock, extraImports, extraRefs),
                ScriptLanguage.FSharp => BuildFsDelegate(scriptBlock), // F# scripting not implemented
                ScriptLanguage.Python => BuildPyDelegate(scriptBlock),
                ScriptLanguage.JavaScript => BuildJsDelegate(scriptBlock),
                _ => throw new NotSupportedException(language.ToString())
            };
            string[] methods = [.. httpVerbs.Select(v => v.ToMethodString())];
            App.MapMethods(pattern, methods, handler).WithLanguage(language);
        }
        catch (CompilationErrorException ex)
        {
            // Log the detailed compilation errors
            Log.Error($"Failed to add route '{pattern}' due to compilation errors:");
            Log.Error(ex.GetDetailedErrorMessage());

            // Re-throw with additional context
            throw new InvalidOperationException(
                $"Failed to compile {language} script for route '{pattern}'. {ex.GetErrors().Count()} error(s) found.",
                ex);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to add route '{pattern}' with method '{string.Join(", ", httpVerbs)}' using {language}: {ex.Message}",
                ex);
        }
    }


    #endregion
    #region Configuration



    public void EnableScheduling()
    {
        if (Log.IsEnabled(LogEventLevel.Debug))
            Log.Debug("EnableScheduling called");
        if (Scheduler == null)
        {
            var runspacepool = CreateRunspacePool(Options.MaxSchedulerRunspaces); // example
            var _log = Log.Logger.ForContext<KestrunHost>();
            Scheduler = new SchedulerService(runspacepool, _log);
        }
        else
        {
            Log.Warning("SchedulerService is already configured, skipping.");
        }
    }

    public void EnableConfiguration()
    {
        if (Log.IsEnabled(LogEventLevel.Debug))
            Log.Debug("EnableConfiguration(options) called");

        if (_isConfigured)
        {
            if (Log.IsEnabled(LogEventLevel.Debug))
                Log.Debug("Configuration already applied, skipping");
            return; // Already configured
        }
        try
        {

            // This method is called to apply the configured options to the Kestrel server.
            // The actual application of options is done in the Run method.
            _runspacePool = CreateRunspacePool(Options.MaxRunspaces);
            if (_runspacePool == null)
            {
                throw new InvalidOperationException("Failed to create runspace pool.");
            }

            //     KestrelServices(builder);
            builder.WebHost.UseKestrel(opts =>
            {
                opts.CopyFromTemplate(Options.ServerOptions);
            });
            builder.WebHost.ConfigureKestrel(kestrelOpts =>
            {
                // Optionally, handle ApplicationName or other properties as needed
                if (Options.Listeners.Count > 0)
                {
                    Options.Listeners.ForEach(opt =>
                    {
                        kestrelOpts.Listen(opt.IPAddress, opt.Port, listenOptions =>
                        {
                            listenOptions.Protocols = opt.Protocols;
                            if (opt.UseHttps && opt.X509Certificate != null)
                                listenOptions.UseHttps(opt.X509Certificate);
                            if (opt.UseConnectionLogging)
                                listenOptions.UseConnectionLogging();
                        });
                    });
                }
            });


            App = Build();
            var dataSource = App.Services.GetRequiredService<EndpointDataSource>();

            if (dataSource.Endpoints.Count == 0)
            {
                Log.Warning("EndpointDataSource is empty. No endpoints configured.");
            }
            else
            {
                foreach (var ep in dataSource.Endpoints)
                {
                    Log.Information("➡️  Endpoint: {DisplayName}", ep.DisplayName);
                }
            }
            if (Options.EnableScheduling)
            {
                EnableScheduling(); // Enable scheduling if needed
                Log.Information("Scheduling enabled.");
            }
            _isConfigured = true;
            Log.Information("Configuration applied successfully.");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error applying configuration: {Message}", ex.Message);
            throw new InvalidOperationException("Failed to apply configuration.", ex);
        }
    }
    #endregion
    #region Builder
    /* More information about the KestrunHost class
    https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.builder.webapplication?view=aspnetcore-8.0

    */

    /// <summary>
    /// Builds the WebApplication.
    /// This method applies all queued services and middleware stages,
    /// and returns the built WebApplication instance.
    /// </summary>
    /// <returns>The built WebApplication.</returns>
    /// <exception cref="InvalidOperationException"></exception>
    public WebApplication Build()
    {
        if (builder == null) throw new InvalidOperationException("Call CreateBuilder() first.");

        // 1️⃣  Apply all queued services
        foreach (var configure in _serviceQueue)
        {
            configure(builder.Services);
        }

        // 2️⃣  Build the WebApplication
        App = builder.Build();

        Log.Information("CWD: {CWD}", Directory.GetCurrentDirectory());
        Log.Information("ContentRoot: {Root}", App.Environment.ContentRootPath);
        var pagesDir = Path.Combine(App.Environment.ContentRootPath, "Pages");
        Log.Information("Pages Dir: {PagesDir}", pagesDir);
        if (Directory.Exists(pagesDir))
        {
            foreach (var file in Directory.GetFiles(pagesDir, "*.*", SearchOption.AllDirectories))
            {
                Log.Information("Pages file: {File}", file);
            }
        }
        else
        {
            Log.Warning("Pages directory does not exist: {PagesDir}", pagesDir);
        }

        // 3️⃣  Apply all queued middleware stages
        foreach (var stage in _middlewareQueue)
        {
            stage(App);
        }
        // 5️⃣  Terminal endpoint execution 
        return App;
    }

    /// <summary>
    /// Adds a service configuration action to the service queue.
    /// This action will be executed when the services are built.
    /// </summary>
    /// <param name="configure">The service configuration action.</param>
    /// <returns>The current KestrunHost instance.</returns>
    public KestrunHost AddService(Action<IServiceCollection> configure)
    {
        _serviceQueue.Add(configure);
        return this;
    }

    /// <summary>
    /// Adds a middleware stage to the application pipeline.
    /// </summary>
    /// <param name="stage">The middleware stage to add.</param>
    /// <returns>The current KestrunHost instance.</returns>
    public KestrunHost Use(Action<IApplicationBuilder> stage)
    {
        _middlewareQueue.Add(stage);
        return this;
    }

    /// <summary>
    /// Adds Razor Pages to the application.
    /// </summary>
    /// <param name="cfg">The configuration options for Razor Pages.</param>
    /// <returns>The current KestrunHost instance.</returns>
    public KestrunHost AddRazorPages(RazorPagesOptions? cfg)
    {
        if (Log.IsEnabled(LogEventLevel.Debug))
            Log.Debug("Adding Razor Pages from source: {Source}", cfg);

        if (cfg == null)
            return AddRazorPages(); // no config, use defaults

        return AddRazorPages(dest =>
            {
                // simple value properties are fine
                dest.RootDirectory = cfg.RootDirectory;

                // copy conventions one‑by‑one (collection is read‑only)
                foreach (var c in cfg.Conventions)
                    dest.Conventions.Add(c);
            });
    }

    /// <summary>
    /// Adds Razor Pages to the application.
    /// This overload allows you to specify configuration options.
    /// If you need to configure Razor Pages options, use the other overload.
    /// </summary>
    /// <param name="cfg">The configuration options for Razor Pages.</param>
    /// <returns>The current KestrunHost instance.</returns>
    public KestrunHost AddRazorPages(Action<RazorPagesOptions>? cfg = null)
    {
        if (Log.IsEnabled(LogEventLevel.Debug))
            Log.Debug("Adding Razor Pages with configuration: {Config}", cfg);
        return AddService(services =>
        {
            var mvc = services.AddRazorPages();         // returns IMvcBuilder

            if (cfg != null)
                mvc.AddRazorPagesOptions(cfg);          // ← the correct extension
                                                        //  —OR—
                                                        // services.Configure(cfg);                 // also works
        })
         // optional: automatically map Razor endpoints after Build()
         .Use(app => ((IEndpointRouteBuilder)app).MapRazorPages());
    }

    /// <summary>
    /// Adds MVC / API controllers to the application.
    /// </summary>
    /// <param name="cfg">The configuration options for MVC / API controllers.</param>
    /// <returns>The current KestrunHost instance.</returns>
    public KestrunHost AddControllers(Action<Microsoft.AspNetCore.Mvc.MvcOptions>? cfg = null)
    {
        return AddService(services =>
        {
            var builder = services.AddControllers();
            if (cfg != null) builder.ConfigureApplicationPartManager(pm => { }); // customise if you wish
        });
    }

    /// <summary>
    /// Adds response compression to the application.
    /// This overload allows you to specify configuration options.
    /// </summary>
    /// <param name="options">The configuration options for response compression.</param>
    /// <returns>The current KestrunHost instance.</returns>
    public KestrunHost AddResponseCompression(ResponseCompressionOptions? options)
    {
        if (Log.IsEnabled(LogEventLevel.Debug))
            Log.Debug("Adding response compression with options: {@Options}", options);
        if (options == null)
            return AddResponseCompression(); // no options, use defaults

        // delegate shim – re‑use the existing pipeline
        return AddResponseCompression(o =>
        {
            o.EnableForHttps = options.EnableForHttps;
            o.MimeTypes = options.MimeTypes;
            o.ExcludedMimeTypes = options.ExcludedMimeTypes;
            // copy provider lists, levels, etc. if you expose them
            foreach (var p in options.Providers) o.Providers.Add(p);
        });
    }

    /// <summary>
    /// Adds response compression to the application.
    /// This overload allows you to specify configuration options.
    /// </summary>
    /// <param name="cfg">The configuration options for response compression.</param>
    /// <returns>The current KestrunHost instance.</returns>
    public KestrunHost AddResponseCompression(Action<ResponseCompressionOptions>? cfg = null)
    {
        if (Log.IsEnabled(LogEventLevel.Debug))
            Log.Debug("Adding response compression with configuration: {Config}", cfg);
        // Service side
        AddService(services =>
        {
            if (cfg == null)
                services.AddResponseCompression();
            else
                services.AddResponseCompression(cfg);
        });

        // Middleware side
        return Use(app => app.UseResponseCompression());
    }

    /// <summary>
    /// Adds static files to the application.
    /// This overload allows you to specify configuration options.
    /// </summary>
    /// <param name="cfg">The static file options to configure.</param>
    /// <returns>The current KestrunHost instance.</returns>
    public KestrunHost AddStaticFiles(Action<StaticFileOptions>? cfg = null)
    {
        if (Log.IsEnabled(LogEventLevel.Debug))
            Log.Debug("Adding static files with configuration: {Config}", cfg);

        return Use(app =>
        {
            if (cfg == null)
                app.UseStaticFiles();
            else
            {
                var options = new StaticFileOptions();
                cfg(options);

                app.UseStaticFiles(options);
            }
        });
    }

    /// <summary>
    /// Adds static files to the application.
    /// This overload allows you to specify configuration options.
    /// </summary>
    /// <param name="options">The static file options to configure.</param>
    /// <returns>The current KestrunHost instance.</returns>
    public KestrunHost AddStaticFiles(StaticFileOptions options)
    {
        if (Log.IsEnabled(LogEventLevel.Debug))
            Log.Debug("Adding static files with options: {@Options}", options);

        if (options == null)
            return AddStaticFiles(); // no options, use defaults

        // reuse the delegate overload so the pipeline logic stays in one place
        return AddStaticFiles(o =>
        {
            // copy only the properties callers are likely to set 
            CopyStaticFileOptions(options, o);

        });
    }

    /// <summary>
    /// Adds antiforgery protection to the application.
    /// This overload allows you to specify configuration options.
    /// </summary>
    /// <param name="options">The antiforgery options to configure.</param>
    /// <returns>The current KestrunHost instance.</returns>
    public KestrunHost AddAntiforgery(AntiforgeryOptions? options)
    {
        if (Log.IsEnabled(LogEventLevel.Debug))
            Log.Debug("Adding Antiforgery with configuration: {@Config}", options);

        if (options == null)
            return AddAntiforgery(); // no config, use defaults

        // Delegate to the Action-based overload
        return AddAntiforgery(cfg =>
        {
            cfg.Cookie = options.Cookie;
            cfg.FormFieldName = options.FormFieldName;
            cfg.HeaderName = options.HeaderName;
            cfg.SuppressXFrameOptionsHeader = options.SuppressXFrameOptionsHeader;
        });
    }

    /// <summary>
    /// Adds antiforgery protection to the application.
    /// </summary>
    /// <param name="setupAction">An optional action to configure the antiforgery options.</param>
    /// <returns>The current KestrunHost instance.</returns>
    public KestrunHost AddAntiforgery(Action<AntiforgeryOptions>? setupAction = null)
    {
        if (Log.IsEnabled(LogEventLevel.Debug))
            Log.Debug("Adding Antiforgery with configuration: {@Config}", setupAction);
        // Service side
        AddService(services =>
        {
            if (setupAction == null)
                services.AddAntiforgery();
            else
                services.AddAntiforgery(setupAction);
        });

        // Middleware side
        return Use(app => app.UseAntiforgery());
    }


    /// <summary>
    /// Adds a CORS policy that allows all origins, methods, and headers.
    /// </summary>
    /// <returns>The current KestrunHost instance.</returns>
    public KestrunHost AddCorsAllowAll() =>
        AddCors("AllowAll", b => b.AllowAnyOrigin()
                                  .AllowAnyMethod()
                                  .AllowAnyHeader());

    /// <summary>
    /// Registers a named CORS policy that was already composed with a
    /// <see cref="CorsPolicyBuilder"/> and applies that policy in the pipeline.
    /// </summary>
    /// <param name="policyName">The name to store/apply the policy under.</param>
    /// <param name="builder">
    ///     A fully‑configured <see cref="CorsPolicyBuilder"/>.
    ///     Callers typically chain <c>.WithOrigins()</c>, <c>.WithMethods()</c>,
    ///     etc. before passing it here.
    /// </param>
    public KestrunHost AddCors(string policyName, CorsPolicyBuilder builder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(policyName);
        ArgumentNullException.ThrowIfNull(builder);

        // 1️⃣ Service‑time registration
        AddService(services =>
        {
            services.AddCors(options =>
            {
                options.AddPolicy(policyName, builder.Build());
            });
        });

        // 2️⃣ Middleware‑time application
        return Use(app => app.UseCors(policyName));
    }

    /// <summary>
    /// Registers a named CORS policy that was already composed with a
    /// <see cref="CorsPolicyBuilder"/> and applies that policy in the pipeline.
    /// </summary>
    /// <param name="policyName">The name to store/apply the policy under.</param>
    /// <param name="buildPolicy">An action to configure the CORS policy.</param>
    /// <returns>The current KestrunHost instance.</returns>
    /// <exception cref="ArgumentException">Thrown when the policy name is null or whitespace.</exception>
    public KestrunHost AddCors(string policyName, Action<CorsPolicyBuilder> buildPolicy)
    {
        if (Log.IsEnabled(LogEventLevel.Debug))
            Log.Debug("Adding CORS policy: {PolicyName}", policyName);
        if (string.IsNullOrWhiteSpace(policyName))
            throw new ArgumentException("Policy name required.", nameof(policyName));
        ArgumentNullException.ThrowIfNull(buildPolicy);

        AddService(s =>
        {
            s.AddCors(o => o.AddPolicy(policyName, buildPolicy));
        });

        // apply only that policy
        return Use(app => app.UseCors(policyName));
    }

    /// <summary>
    /// Adds a PowerShell runtime to the application.
    /// This middleware allows you to execute PowerShell scripts in response to HTTP requests.
    /// </summary>
    /// <param name="routePrefix">The route prefix to use for the PowerShell runtime.</param>
    /// <returns>The current KestrunHost instance.</returns>
    public KestrunHost AddPowerShellRuntime(PathString? routePrefix = null)
    {
        if (Log.IsEnabled(LogEventLevel.Debug))
            Log.Debug("Adding PowerShell runtime with route prefix: {RoutePrefix}", routePrefix);
        return Use(app =>
        {
            ArgumentNullException.ThrowIfNull(_runspacePool);
            if (routePrefix.HasValue)
            {
                // ── mount PowerShell only under /ps (or whatever you pass) ──
                app.Map(routePrefix.Value, branch =>
                {
                    branch.UseLanguageRuntime(
                        ScriptLanguage.PowerShell,
                        b => b.UsePowerShellRunspace(_runspacePool));
                });
            }
            else
            {
                // ── mount PowerShell at the root ──
                app.UseLanguageRuntime(
                    ScriptLanguage.PowerShell,
                    b => b.UsePowerShellRunspace(_runspacePool));
            }
        });
    }


    /// <summary>
    /// Adds PowerShell Razor Pages to the application.
    /// This middleware allows you to serve Razor Pages using PowerShell scripts.
    /// </summary>
    /// <param name="routePrefix">The route prefix to use for the PowerShell Razor Pages.</param>
    /// <param name="cfg">Configuration options for the Razor Pages.</param>
    /// <returns>The current KestrunHost instance.</returns>
    public KestrunHost AddPowerShellRazorPages(PathString? routePrefix, RazorPagesOptions? cfg)
    {
        if (Log.IsEnabled(LogEventLevel.Debug))
            Log.Debug("Adding PowerShell Razor Pages with route prefix: {RoutePrefix}, config: {@Config}", routePrefix, cfg);

        return AddPowerShellRazorPages(routePrefix, dest =>
            {
                if (cfg != null)
                {
                    // simple value properties are fine
                    dest.RootDirectory = cfg.RootDirectory;

                    // copy conventions one‑by‑one (collection is read‑only)
                    foreach (var c in cfg.Conventions)
                        dest.Conventions.Add(c);
                }
            });
    }

    /// <summary>
    /// Adds PowerShell Razor Pages to the application.
    /// This middleware allows you to serve Razor Pages using PowerShell scripts.
    /// </summary>
    /// <param name="routePrefix">The route prefix to use for the PowerShell Razor Pages.</param>
    /// <returns>The current KestrunHost instance.</returns>
    public KestrunHost AddPowerShellRazorPages(PathString? routePrefix) =>
        AddPowerShellRazorPages(routePrefix, (Action<RazorPagesOptions>?)null);
    public KestrunHost AddPowerShellRazorPages() =>
        AddPowerShellRazorPages(null, (Action<RazorPagesOptions>?)null);
    // helper: true  ⇢ file contains managed metadata
    static bool IsManaged(string path)
    {
        try { _ = AssemblyName.GetAssemblyName(path); return true; }
        catch { return false; }          // native ⇒ BadImageFormatException
    }
    /// <summary>
    /// Adds PowerShell Razor Pages to the application.
    /// This middleware allows you to serve Razor Pages using PowerShell scripts.
    /// </summary>
    /// <param name="routePrefix">The route prefix to use for the PowerShell Razor Pages.</param>
    /// <param name="cfg">Configuration options for the Razor Pages.</param>
    /// <returns>The current KestrunHost instance.</returns>
    public KestrunHost AddPowerShellRazorPages(PathString? routePrefix = null, Action<RazorPagesOptions>? cfg = null)
    {
        if (Log.IsEnabled(LogEventLevel.Debug))
            Log.Debug("Adding PowerShell Razor Pages with route prefix: {RoutePrefix}, config: {@Config}", routePrefix, cfg);
        /*AddService(services =>
        {
            if (Log.IsEnabled(LogEventLevel.Debug))
                Log.Debug("Adding PowerShell Razor Pages to the service with route prefix: {RoutePrefix}", routePrefix);
            var mvc = services.AddRazorPages();

            // ← this line makes the loose .cshtml files discoverable at runtime
            mvc.AddRazorRuntimeCompilation();
            if (cfg != null)
                mvc.AddRazorPagesOptions(cfg);
        });*/

        AddService(services =>
                {
                    var env = builder.Environment;
                    /*         var csFiles = Directory.GetFiles(Path.Combine(env.ContentRootPath, "Pages", "cs"),
                                                       "*.cshtml.cs", SearchOption.AllDirectories);

                      var trees = csFiles.Select(f => CSharpSyntaxTree.ParseText(File.ReadAllText(f)));

                      var refs = AppDomain.CurrentDomain.GetAssemblies()
                                     .Where(a => !a.IsDynamic && File.Exists(a.Location))
                                     .Select(a => MetadataReference.CreateFromFile(a.Location));

                      var comp = CSharpCompilation.Create("DynamicPages",
                                     trees, refs,
                                     new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

                      using var ms = new MemoryStream();
                      var emit = comp.Emit(ms);                 // <- returns EmitResult

                      if (!emit.Success)
                      {
                          foreach (var d in emit.Diagnostics)
                              Log.Error(d.ToString());                 // or Console.WriteLine …
                          return;                                      // abort start-up
                      }
                      ms.Position = 0;

                      var bytes = ms.ToArray();

                      // ① write DLL + (optionally) PDB to a temp location
                      var tmpDir = Path.Combine(Path.GetTempPath(), "KestrunDynamic");
                      Directory.CreateDirectory(tmpDir);

                      var dllPath = Path.Combine(tmpDir, "DynamicPages.dll");
                      File.WriteAllBytes(dllPath, bytes);

                      // ② load it so the types are available to MVC
                      var pagesAsm = Assembly.Load(bytes);

                      // ③ register with MVC & RuntimeCompilation
                      services.AddRazorPages()
                              .AddApplicationPart(pagesAsm)                       // exposes PageModels
                              .AddRazorRuntimeCompilation(o =>
                                   o.AdditionalReferencePaths.Add(dllPath));      // lets Roslyn find it
  */

                    services.AddRazorPages().AddRazorRuntimeCompilation();

                    // ── NEW: feed Roslyn every assembly already loaded ──────────
                    //      var env = builder.Environment;                  // or app.Environment
                    var pagesRoot = Path.Combine(env.ContentRootPath, "Pages");

                    services.Configure<MvcRazorRuntimeCompilationOptions>(opts =>
                    {
                        // 1️⃣  everything that’s already loaded and managed
                        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies()
                                         .Where(a => !a.IsDynamic && IsManaged(a.Location)))
                            opts.AdditionalReferencePaths.Add(asm.Location);

                        // 2️⃣  managed DLLs from the .NET-8 shared-framework folder
                        var coreDir = Path.GetDirectoryName(typeof(object).Assembly.Location)!;   // e.g. …\dotnet\shared\Microsoft.NETCore.App\8.0.x
                        foreach (var dll in Directory.EnumerateFiles(coreDir, "*.dll")
                                                     .Where(IsManaged))
                            opts.AdditionalReferencePaths.Add(dll);

                        // 3️⃣  (optional) watch your project’s Pages folder so edits hot-reload
                        var pagesRoot = Path.Combine(builder.Environment.ContentRootPath, "Pages");
                        if (Directory.Exists(pagesRoot))
                            opts.FileProviders.Add(new PhysicalFileProvider(pagesRoot));
                    });
                });

        // 1️⃣  add everything *before* ApplyConfiguration()
        /*   AddService(services =>
           {
               // ---- dynamic compile of *.cshtml.cs --------------------
               var env = builder.Environment;
               var pagesDir = Path.Combine(env.ContentRootPath, "Pages", "cs");
               var trees = Directory.EnumerateFiles(pagesDir, "*.cshtml.cs", SearchOption.AllDirectories)
                                       .Select(f => CSharpSyntaxTree.ParseText(File.ReadAllText(f)));

               var refs = AppDomain.CurrentDomain.GetAssemblies()
                               .Where(a => !a.IsDynamic && File.Exists(a.Location))
                               .Select(a => MetadataReference.CreateFromFile(a.Location));

               var comp = CSharpCompilation.Create(
                               "DynamicPages",
                               trees, refs,
                               new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

               using var ms = new MemoryStream();
               var result = comp.Emit(ms);

               if (!result.Success)                       // ← only abort *here* on failure
               {
                   foreach (var d in result.Diagnostics)
                       Log.Error(d.ToString());
                   throw new InvalidOperationException("Page compilation failed");
               }

               ms.Position = 0;
               var pagesAsm = Assembly.Load(ms.ToArray());

               // 2️⃣  register Razor-Pages *and* the dynamic assembly
               services.AddRazorPages()
                       .AddApplicationPart(pagesAsm)
                       .AddRazorRuntimeCompilation(o =>
                       {
                           // allow Razor to reference the in-memory assembly again
                           var tmp = Path.Combine(Path.GetTempPath(), "DynamicPages.dll");
                           File.WriteAllBytes(tmp, ms.ToArray());      // sync, no ‘await’ needed
                           o.AdditionalReferencePaths.Add(tmp);
                       });
           });
   */

        return Use(app =>
        {
            ArgumentNullException.ThrowIfNull(_runspacePool);
            if (Log.IsEnabled(LogEventLevel.Debug))
                Log.Debug("Adding PowerShell Razor Pages middleware with route prefix: {RoutePrefix}", routePrefix);


            if (routePrefix.HasValue)
            {
                // ── /ps  (or whatever prefix) ──────────────────────────────
                app.Map(routePrefix.Value, branch =>
                {
                    branch.UsePowerShellRazorPages(_runspacePool);   // bridge
                    branch.UseRouting();                             // add routing
                    branch.UseEndpoints(e => e.MapRazorPages());     // map pages
                });
            }
            else
            {
                // ── mounted at root ────────────────────────────────────────
                app.UsePowerShellRazorPages(_runspacePool);          // bridge
                app.UseRouting();                                    // add routing
                app.UseEndpoints(e => e.MapRazorPages());            // map pages

                /*   app.Use(async (ctx, next) =>
   {
       var ds = ctx.GetEndpoint();          // null at build time
       Console.WriteLine($"Endpoints now: {ctx.RequestServices
           .GetRequiredService<EndpointDataSource>().Endpoints.Count}");
       await next();
   });*/

            }

            if (Log.IsEnabled(LogEventLevel.Debug))
                Log.Debug("PowerShell Razor Pages middleware added with route prefix: {RoutePrefix}", routePrefix);
        });
    }

    /// <summary>
    /// Adds default files middleware to the application.
    /// This middleware serves default files like index.html when a directory is requested.
    /// </summary>
    /// <param name="cfg">Configuration options for the default files middleware.</param>
    /// <returns>The current KestrunHost instance.</returns>
    public KestrunHost AddDefaultFiles(DefaultFilesOptions? cfg)
    {
        if (Log.IsEnabled(LogEventLevel.Debug))
            Log.Debug("Adding Default Files with configuration: {@Config}", cfg);

        if (cfg == null)
            return AddDefaultFiles(); // no config, use defaults

        // Convert DefaultFilesOptions to an Action<DefaultFilesOptions>
        return AddDefaultFiles(options =>
        {
            CopyDefaultFilesOptions(cfg, options);
        });
    }

    /// <summary>
    /// Adds default files middleware to the application.
    /// This middleware serves default files like index.html when a directory is requested.
    /// </summary>
    /// <param name="cfg">Configuration options for the default files middleware.</param>
    /// <returns>The current KestrunHost instance.</returns>
    public KestrunHost AddDefaultFiles(Action<DefaultFilesOptions>? cfg = null)
    {
        return Use(app =>
        {
            var options = new DefaultFilesOptions();
            cfg?.Invoke(options);
            app.UseDefaultFiles(options);
        });
    }

    /// <summary>
    /// Copies static file options from one object to another.
    /// </summary>
    /// <param name="src">The source static file options.</param>
    /// <param name="dest">The destination static file options.</param>
    /// <remarks>
    /// This method copies properties from the source static file options to the destination static file options.
    /// </remarks>
    private static void CopyStaticFileOptions(StaticFileOptions? src, StaticFileOptions dest)
    {
        // If no source, return a new empty options object
        if (src == null || dest == null) return;
        // Copy properties from source to destination
        dest.ContentTypeProvider = src.ContentTypeProvider;
        dest.OnPrepareResponse = src.OnPrepareResponse;
        dest.ServeUnknownFileTypes = src.ServeUnknownFileTypes;
        dest.DefaultContentType = src.DefaultContentType;
        dest.FileProvider = src.FileProvider;
        dest.RequestPath = src.RequestPath;
        dest.RedirectToAppendTrailingSlash = src.RedirectToAppendTrailingSlash;
        dest.HttpsCompression = src.HttpsCompression;
    }

    /// <summary>
    /// Copies default files options from one object to another.
    /// This method is used to ensure that the default files options are correctly configured.
    /// </summary>
    /// <param name="src">The source default files options.</param>
    /// <param name="dest">The destination default files options.</param>
    /// <remarks>
    /// This method copies properties from the source default files options to the destination default files options.   
    /// </remarks>
    private static void CopyDefaultFilesOptions(DefaultFilesOptions? src, DefaultFilesOptions dest)
    {
        // If no source, return a new empty options object
        if (src == null || dest == null) return;
        // Copy properties from source to destination 
        dest.DefaultFileNames.Clear();
        foreach (var name in src.DefaultFileNames)
            dest.DefaultFileNames.Add(name);
        dest.FileProvider = src.FileProvider;
        dest.RequestPath = src.RequestPath;
        dest.RedirectToAppendTrailingSlash = src.RedirectToAppendTrailingSlash;
    }

    /// <summary>
    /// Adds a file server middleware to the application.   
    /// This middleware serves static files and default files from a specified file provider.
    /// </summary>
    /// <param name="cfg">Configuration options for the file server middleware.</param>
    /// <returns>The current KestrunHost instance.</returns>
    /// </remarks>
    /// This method allows you to configure the file server options such as enabling default files, directory browsing,
    /// and setting the file provider and request path.
    /// </remarks>
    public KestrunHost AddFileServer(FileServerOptions? cfg)
    {
        if (Log.IsEnabled(LogEventLevel.Debug))
            Log.Debug("Adding File Server with configuration: {@Config}", cfg);
        if (cfg == null)
            return AddFileServer(); // no config, use defaults

        // Convert FileServerOptions to an Action<FileServerOptions>
        return AddFileServer(options =>
        {
            options.EnableDefaultFiles = cfg.EnableDefaultFiles;
            options.EnableDirectoryBrowsing = cfg.EnableDirectoryBrowsing;
            options.FileProvider = cfg.FileProvider;
            options.RequestPath = cfg.RequestPath;
            options.RedirectToAppendTrailingSlash = cfg.RedirectToAppendTrailingSlash;
            CopyDefaultFilesOptions(cfg.DefaultFilesOptions, options.DefaultFilesOptions);
            if (cfg.DirectoryBrowserOptions != null)
            {
                options.DirectoryBrowserOptions.FileProvider = cfg.DirectoryBrowserOptions.FileProvider;
                options.DirectoryBrowserOptions.RequestPath = cfg.DirectoryBrowserOptions.RequestPath;
                options.DirectoryBrowserOptions.RedirectToAppendTrailingSlash = cfg.DirectoryBrowserOptions.RedirectToAppendTrailingSlash;
            }

            CopyStaticFileOptions(cfg.StaticFileOptions, options.StaticFileOptions);
        });
    }

    /// <summary>
    /// Adds a file server middleware to the application.
    /// This middleware serves static files and default files from a specified file provider.
    /// </summary>
    /// <param name="cfg">Configuration options for the file server middleware.</param>
    /// <returns>The current KestrunHost instance.</returns>
    public KestrunHost AddFileServer(Action<FileServerOptions>? cfg = null)
    {
        if (Log.IsEnabled(LogEventLevel.Debug))
            Log.Debug("Adding File Server with configuration: {@Config}", cfg);
        return Use(app =>
        {
            var options = new FileServerOptions();
            cfg?.Invoke(options);
            app.UseFileServer(options);
        });
    }


    /*
public KestrunHost AddJwtAuth(Action<JwtBearerOptions> cfg)
    {
        return AddService(s =>
        {
            s.AddAuthentication("Bearer")
             .AddJwtBearer("Bearer", cfg);
        })
        .Use(app => app.UseAuthentication());   // auth middleware
    }*/

    // ② SignalR
    public KestrunHost AddSignalR<T>(string path) where T : Hub
    {
        return AddService(s => s.AddSignalR())
               .Use(app => ((IEndpointRouteBuilder)app).MapHub<T>(path));
    }

    /*
        // ④ gRPC
        public KestrunHost AddGrpc<TService>() where TService : class
        {
            return AddService(s => s.AddGrpc())
                   .Use(app => app.MapGrpcService<TService>());
        }
    */

    /*   public KestrunHost AddSwagger()
       {
           AddService(s =>
           {
               s.AddEndpointsApiExplorer();
               s.AddSwaggerGen();
           });
           //  ⚠️ Swagger’s middleware normally goes first in the pipeline
           return Use(app =>
           {
               app.UseSwagger();
               app.UseSwaggerUI();
           });
       }*/

    // Add as many tiny helpers as you wish:
    // • AddAuthentication(jwt => { … })
    // • AddSignalR()
    // • AddHealthChecks()
    // • AddGrpc()
    // etc.

    #endregion
    #region Run/Start/Stop

    public void Run()
    {
        if (Log.IsEnabled(LogEventLevel.Debug))
            Log.Debug("Run() called");
        EnableConfiguration();

        App?.Run();
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (Log.IsEnabled(LogEventLevel.Debug))
            Log.Debug("StartAsync() called");
        EnableConfiguration();
        if (App != null)
        {
            await App.StartAsync(cancellationToken);
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (Log.IsEnabled(LogEventLevel.Debug))
            Log.Debug("StopAsync() called");
        if (App != null)
        {
            await App.StopAsync(cancellationToken);
        }
    }

    public void Stop()
    {
        if (Log.IsEnabled(LogEventLevel.Debug))
            Log.Debug("Stop() called");
        // This initiates a graceful shutdown.
        App?.Lifetime.StopApplication();
    }

    #endregion



    #region Runspace Pool Management



    private KestrunRunspacePoolManager CreateRunspacePool(int? maxRunspaces = 0)
    {
        if (Log.IsEnabled(LogEventLevel.Debug))
            Log.Debug("CreateRunspacePool() called: {@MaxRunspaces}", maxRunspaces);

        // Create a default InitialSessionState with an unrestricted policy:
        var iss = InitialSessionState.CreateDefault();

        iss.ExecutionPolicy = Microsoft.PowerShell.ExecutionPolicy.Unrestricted;

        foreach (var p in _modulePaths)
        {
            iss.ImportPSModule([p]);
        }

        // Inject global variables into all runspaces
        foreach (var kvp in SharedState.Snapshot())
        {
            // kvp.Key = "Visits", kvp.Value = 0
            iss.Variables.Add(
                new SessionStateVariableEntry(
                    kvp.Key,
                    kvp.Value,
                    "Global variable"
                )
            );
        }
        int maxRs = (maxRunspaces.HasValue && maxRunspaces.Value > 0) ? maxRunspaces.Value : Environment.ProcessorCount * 2;

        Log.Information($"Creating runspace pool with max runspaces: {maxRs}");
        var runspacePool = new KestrunRunspacePoolManager(Options?.MinRunspaces ?? 1, maxRunspaces: maxRs, initialSessionState: iss);
        // Return the created runspace pool
        return runspacePool;
    }


    #endregion


    #region Disposable

    public void Dispose()
    {
        if (Log.IsEnabled(LogEventLevel.Debug))
            Log.Debug("Dispose() called");
        _runspacePool?.Dispose();
        _runspacePool = null; // Clear the runspace pool reference
        _isConfigured = false; // Reset configuration state 
        App = null;
        Scheduler?.Dispose();
        Log.CloseAndFlush();
    }
    #endregion

    #region Script Validation

    /// <summary>
    /// Validates a C# script and returns compilation diagnostics without throwing exceptions.
    /// Useful for testing scripts before adding routes.
    /// </summary>
    /// <param name="code">The C# script code to validate</param>
    /// <param name="extraImports">Optional additional imports</param>
    /// <param name="extraRefs">Optional additional assembly references</param>
    /// <param name="languageVersion">C# language version to use</param>
    /// <returns>Compilation diagnostics including errors and warnings</returns>
    public ImmutableArray<Diagnostic> ValidateCSharpScript(
        string? code,
        string[]? extraImports = null,
        Assembly[]? extraRefs = null,
        LanguageVersion languageVersion = LanguageVersion.CSharp12)
    {
        if (Log.IsEnabled(LogEventLevel.Debug))
            Log.Debug("ValidateCSharpScript() called: {@CodeLength}, imports={ImportsCount}, refs={RefsCount}, lang={Lang}",
                code?.Length, extraImports?.Length ?? 0, extraRefs?.Length ?? 0, languageVersion);
        try
        {
            // Use the same script options as BuildCsDelegate
            var opts = ScriptOptions.Default
                       .WithImports("System", "System.Linq", "System.Threading.Tasks", "Microsoft.AspNetCore.Http")
                       .WithReferences(typeof(HttpContext).Assembly, typeof(KestrunResponse).Assembly)
                       .WithLanguageVersion(languageVersion);

            if (extraImports is { Length: > 0 })
                opts = opts.WithImports(opts.Imports.Concat(extraImports));

            if (extraRefs is { Length: > 0 })
                opts = opts.WithReferences(opts.MetadataReferences
                                              .Concat(extraRefs.Select(r => MetadataReference.CreateFromFile(r.Location))));

            var script = CSharpScript.Create(code, opts, typeof(CsGlobals));
            return script.Compile();
        }
        catch (Exception ex)
        {
            // If there's an exception during script creation, create a synthetic diagnostic
            var diagnostic = Diagnostic.Create(
                new DiagnosticDescriptor(
                    "KESTRUN001",
                    "Script validation error",
                    "Script validation failed: {0}",
                    "Compilation",
                    DiagnosticSeverity.Error,
                    true),
                Location.None,
                ex.Message);

            return ImmutableArray.Create(diagnostic);
        }
    }

    /// <summary>
    /// Checks if a C# script has compilation errors.
    /// </summary>
    /// <param name="code">The C# script code to check</param>
    /// <param name="extraImports">Optional additional imports</param>
    /// <param name="extraRefs">Optional additional assembly references</param>
    /// <param name="languageVersion">C# language version to use</param>
    /// <returns>True if the script compiles without errors, false otherwise</returns>
    public bool IsCSharpScriptValid(
        string code,
        string[]? extraImports = null,
        Assembly[]? extraRefs = null,
        LanguageVersion languageVersion = LanguageVersion.CSharp12)
    {
        var diagnostics = ValidateCSharpScript(code, extraImports, extraRefs, languageVersion);
        return !diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error);
    }

    /// <summary>
    /// Gets formatted error information for a C# script.
    /// </summary>
    /// <param name="code">The C# script code to check</param>
    /// <param name="extraImports">Optional additional imports</param>
    /// <param name="extraRefs">Optional additional assembly references</param>
    /// <param name="languageVersion">C# language version to use</param>
    /// <returns>Formatted error message, or null if no errors</returns>
    public string? GetCSharpScriptErrors(
        string code,
        string[]? extraImports = null,
        Assembly[]? extraRefs = null,
        LanguageVersion languageVersion = LanguageVersion.CSharp12)
    {
        if (Log.IsEnabled(LogEventLevel.Debug))
            Log.Debug("GetCSharpScriptErrors() called: {@CodeLength}, imports={ImportsCount}, refs={RefsCount}, lang={Lang}",
                code?.Length, extraImports?.Length ?? 0, extraRefs?.Length ?? 0, languageVersion);

        var diagnostics = ValidateCSharpScript(code, extraImports, extraRefs, languageVersion);
        var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();

        if (errors.Length == 0)
            return null;

        try
        {
            // Create a temporary exception to format the errors
            var tempException = new CompilationErrorException("Script validation errors:", diagnostics);
            return tempException.GetDetailedErrorMessage();
        }
        catch
        {
            // Fallback formatting if exception creation fails
            var sb = new StringBuilder();
            sb.AppendLine($"Script has {errors.Length} compilation error(s):");
            for (int i = 0; i < errors.Length; i++)
            {
                sb.AppendLine($"  {i + 1}. {errors[i].GetMessage()}");
            }
            return sb.ToString();
        }
    }

    #endregion
}
