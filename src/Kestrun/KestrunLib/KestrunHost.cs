using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using FSharp.Compiler.Interactive;
using Microsoft.FSharp.Core;
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
using KestrunLib;
using Microsoft.CodeAnalysis;
using System.Reflection;
using Microsoft.CodeAnalysis.CSharp;
using System.Security.Cryptography.X509Certificates;
using System.Security;
using Microsoft.AspNetCore.Hosting;
using Serilog;
using Serilog.Events;


namespace KestrumLib
{
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

        private KestrunOptions? _kestrelOptions;
        private readonly List<string> _modulePaths = new();

        private bool _isConfigured = false;

        private RunspacePool? _runspacePool;
        public string KestrunRoot { get; private set; }
        #endregion


        // Accepts optional module paths (from PowerShell)
        #region Constructor
        /// <summary>
        /// Creates a new Kestrun host instance.
        /// </summary>
        /// <param name="appName">Optional application name used for logging.</param>
        /// <param name="kestrunRoot">Root directory where scripts are located.</param>
        /// <param name="modulePathsObj">Additional PowerShell module paths.</param>
        public KestrunHost(string? appName = null, string? kestrunRoot = null, string[]? modulePathsObj = null)
        {
            KestrunRoot = kestrunRoot ?? Directory.GetCurrentDirectory();
            Directory.SetCurrentDirectory(KestrunRoot);
            // Configure Serilog logger
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug().MinimumLevel.Verbose()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
                .Enrich.FromLogContext()
                // .WriteTo.Console()
                .WriteTo.File("logs/kestrun.log", rollingInterval: RollingInterval.Day)
                .CreateLogger();
            // Log constructor entry
            if (Log.IsEnabled(LogEventLevel.Debug))
                Log.Debug("KestrunHost constructor called with appName: {AppName},  kestrunRoot: {KestrunRoot}, modulePathsObj length: {ModulePathsLength}", appName, KestrunRoot, modulePathsObj?.Length ?? 0);

            builder = WebApplication.CreateBuilder();
            // Add Serilog to ASP.NET Core logging
            builder.Host.UseSerilog();

            if (!string.IsNullOrEmpty(appName))
            {
                Log.Information("Setting application name: {AppName}", appName);
                _kestrelOptions = new KestrunOptions { ApplicationName = appName };
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
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to configure response compression.", ex);
            }
        }




        #region ListenerOptions

        private List<ListenerOptions>? _listenerOptions;


        /// <summary>
        /// Adds a listener for the specified port and IP address.
        /// </summary>
        public void ConfigureListener(int port, IPAddress? ipAddress = null, X509Certificate2? x509Certificate = null, HttpProtocols protocols = HttpProtocols.Http1, bool useConnectionLogging = false)
        {
            if (Log.IsEnabled(LogEventLevel.Debug))
                Log.Debug("ConfigureListener port={Port}, ipAddress={IPAddress}, protocols={Protocols}, useConnectionLogging={UseConnectionLogging}, certificate supplied={HasCert}", port, ipAddress, protocols, useConnectionLogging, x509Certificate != null);

            _listenerOptions ??= [];
            _listenerOptions.Add(new ListenerOptions
            {
                IPAddress = ipAddress ?? IPAddress.Any,
                Port = port,
                UseHttps = x509Certificate != null,
                X509Certificate = x509Certificate,
                Protocols = protocols,
                UseConnectionLogging = useConnectionLogging
            });

        }

        /// <summary>
        /// Convenience overload that configures an HTTP listener without TLS.
        /// </summary>
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


        /// <summary>
        /// Sets the path to the Python runtime DLL used by Python.NET.
        /// </summary>
        /// <param name="path">Full filesystem path to pythonXY.dll.</param>
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
                    pyHandle(context, krResponse);

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


        private RequestDelegate BuildFsDelegate(string code)
        { // F# scripting not implemented yet
            if (Log.IsEnabled(LogEventLevel.Debug))
                Log.Debug("Building F# delegate, script length={Length}", code?.Length);
            throw new NotImplementedException("F# scripting is not yet supported in Kestrun.");
        }


        #region C#

        // ---------------------------------------------------------------------------
        //  C# delegate builder  –  now takes optional imports / references
        // ---------------------------------------------------------------------------
        public record CsGlobals(KestrunRequest Request, KestrunResponse Response);

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
            extraImports ??= ["KestrumLib"];
            if (!extraImports.Contains("KestrumLib"))
            {
                var importsList = extraImports.ToList();
                importsList.Add("KestrumLib");
                extraImports = [.. importsList];
            }
            if (extraImports is { Length: > 0 })
                opts = opts.WithImports(opts.Imports.Concat(extraImports));

            if (extraRefs is { Length: > 0 })
                opts = opts.WithReferences(opts.MetadataReferences
                                              .Concat(extraRefs.Select(r => MetadataReference.CreateFromFile(r.Location))));

            // 2. Compile once
            var script = CSharpScript.Create(code, opts, typeof(CsGlobals));
            var diagnostics = script.Compile();

            // Check for compilation errors
            if (diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error))
            {
                throw new CompilationErrorException("C# route code compilation failed", diagnostics);
            }

            // Log warnings if any (optional - for debugging)
            var warnings = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Warning).ToArray();
            if (warnings.Any())
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
                    await script.RunAsync(new CsGlobals(krRequest, krResponse)).ConfigureAwait(false);

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



        public sealed class PowerShellRunspaceMiddleware(RequestDelegate next, RunspacePool pool)
        {

            private readonly RequestDelegate _next = next ?? throw new ArgumentNullException(nameof(next));
            private readonly RunspacePool _pool = pool ?? throw new ArgumentNullException(nameof(pool));

            public async Task InvokeAsync(HttpContext context)
            {
                try
                {
                    EnsureRunspacePoolOpen(_pool);
                    // Acquire a runspace from the pool and keep it for the whole request
                    using PowerShell ps = PowerShell.Create();
                    ps.RunspacePool = _pool;
                    var krRequest = await KestrunRequest.NewRequest(context);
                    var krResponse = new KestrunResponse(krRequest);

                    // keep a reference for any C# code later in the pipeline
                    context.Items[KR_REQUEST_KEY] = krRequest;
                    context.Items[KR_RESPONSE_KEY] = krResponse;
                    // Set the PowerShell variables for the request and response
                    //  var ss = ps.Runspace.SessionStateProxy; 
                    //ss.SetVariable("Request", krReq);
                    // ss.SetVariable("Response", krResp);
                    Log.Verbose("Setting PowerShell variables for Request and Response in the runspace.");
                    ps.AddCommand("Set-Variable")
                        .AddParameter("Name", "Request")
                        .AddParameter("Value", krRequest)
                        .AddParameter("Scope", "Script")
                        .AddStatement()
                        .AddCommand("Set-Variable")
                        .AddParameter("Name", "Response")
                        .AddParameter("Value", krResponse)
                        .AddParameter("Scope", "Script");

                 /*   foreach (var kv in GlobalVariables.GetAllValues())
                    {
                        ps.AddCommand("Set-Variable")
                          .AddParameter("Name", kv.Key)
                          .AddParameter("Value", kv.Value ?? PSObject.AsPSObject(null)) // handle nulls
                          .AddParameter("Scope", "Script")
                          .AddStatement();
                    }*/
                    // Run this once to inject variables into the runspace 
                    ps.Invoke();
                    // clear the commands so you can use ps.Invoke/InvokeAsync again later:
                    ps.Commands.Clear();
                    context.Items[PS_INSTANCE_KEY] = ps;
                    try
                    {
                        await _next(context);                // continue the pipeline

                    /*    foreach (var name in GlobalVariables.GetAllValues().Keys)
                        {
                            ps.AddCommand("Get-Variable")
                              .AddParameter("Name", name)
                              .AddParameter("Scope", "Script");
                            var results = ps.Invoke();
                            ps.Commands.Clear();

                            if (results.Count > 0)
                            {
                                var newVal = results[0].BaseObject;
                                GlobalVariables.UpdateValue(name, newVal);
                            }
                        }*/
                    }
                    finally
                    {
                        if (ps != null)
                        {
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
                if (ps.RunspacePool == null)
                {
                    throw new InvalidOperationException("PowerShell runspace pool is not set. Ensure PowerShellRunspaceMiddleware is registered.");
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
                    var psResults = await Task.Run(() => ps.Invoke())               // no pool dead-lock
                    .ConfigureAwait(false);
                    //  var psResults = ps.Invoke();
                    //var psResults = await ps.InvokeAsync().ConfigureAwait(false);

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
                        BuildError.Text(ps);
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

        /// <summary>
        /// Adds a native .NET route handler for a single HTTP verb.
        /// </summary>
        public void AddNativeRoute(string pattern, HttpVerb httpVerb, KestrunHandler handler)
        {
            if (Log.IsEnabled(LogEventLevel.Debug))
                Log.Debug("AddNativeRoute called with pattern={Pattern}, httpVerb={HttpVerb}", pattern, httpVerb);
            AddNativeRoute(pattern: pattern, httpVerbs: [httpVerb], handler: handler);
        }

        /// <summary>
        /// Adds a native .NET route handler supporting multiple HTTP verbs.
        /// </summary>
        public void AddNativeRoute(string pattern, IEnumerable<HttpVerb> httpVerbs, KestrunHandler handler)
        {
            if (Log.IsEnabled(LogEventLevel.Debug))
                Log.Debug("AddNativeRoute called with pattern={Pattern}, httpVerbs={HttpVerbs}", pattern, string.Join(", ", httpVerbs));
            if (App is null)
                throw new InvalidOperationException("WebApplication is not initialized. Call ApplyConfiguration first.");
            string[] methods = [.. httpVerbs.Select(v => v.ToMethodString())];
            App.MapMethods(pattern, methods, async context =>
            {
                var req = await KestrunRequest.NewRequest(context);
                var res = new KestrunResponse(req);
                await handler(req, res);
                await res.ApplyTo(context.Response);
            });
        }

        /// <summary>
        /// Adds a script-based route for a single HTTP verb.
        /// </summary>
        public void AddRoute(string pattern,
                                         HttpVerb httpVerbs,
                                          string scriptBlock,
                                          ScriptLanguage language = ScriptLanguage.PowerShell,
                                          string[]? extraImports = null,
                                          Assembly[]? extraRefs = null)
        {
            AddRoute(pattern, [httpVerbs], scriptBlock, language, extraImports, extraRefs);
        }

        /// <summary>
        /// Adds a script-based route supporting multiple HTTP verbs.
        /// </summary>
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
                    "WebApplication is not initialized. Call ApplyConfiguration first.");
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
        /// <summary>
        /// Stores Kestrel configuration options to be applied later.
        /// </summary>
        public void ConfigureKestrel(KestrunOptions options)
        {
            _kestrelOptions = options;
        }

        /// <summary>
        /// Applies previously configured options and initializes services.
        /// </summary>
        public void ApplyConfiguration()
        {
            if (Log.IsEnabled(LogEventLevel.Debug))
                Log.Debug("ApplyConfiguration() called");
            if (_kestrelOptions == null)
            {
                _kestrelOptions = new KestrunOptions();
            }
            ApplyConfiguration(_kestrelOptions);
        }

        /// <summary>
        /// Applies the specified options to the underlying web host.
        /// </summary>
        public void ApplyConfiguration(KestrunOptions options)
        {
            if (Log.IsEnabled(LogEventLevel.Debug))
                Log.Debug("ApplyConfiguration(options) called: {@Options}", options);

            if (_isConfigured)
            {
                if (Log.IsEnabled(LogEventLevel.Debug))
                    Log.Debug("Configuration already applied, skipping");
                return; // Already configured
            }

            // This method is called to apply the configured options to the Kestrel server.
            // The actual application of options is done in the Run method.
            _runspacePool = CreateRunspacePool(options.MaxRunspaces);
            if (_runspacePool == null)
            {
                throw new InvalidOperationException("Failed to create runspace pool.");
            }
            _runspacePool.Open();
            KestrelServices(builder);

            builder.WebHost.ConfigureKestrel(kestrelOpts =>
            {
                if (options.AllowSynchronousIO.HasValue)
                    kestrelOpts.AllowSynchronousIO = options.AllowSynchronousIO.Value;
                if (options.AllowResponseHeaderCompression.HasValue)
                    kestrelOpts.AllowResponseHeaderCompression = options.AllowResponseHeaderCompression.Value;
                if (options.AddServerHeader.HasValue)
                    kestrelOpts.AddServerHeader = options.AddServerHeader.Value;
                if (options.AllowHostHeaderOverride.HasValue)
                    kestrelOpts.AllowHostHeaderOverride = options.AllowHostHeaderOverride.Value;
                if (options.AllowAlternateSchemes.HasValue)
                    kestrelOpts.AllowAlternateSchemes = options.AllowAlternateSchemes.Value;
                if (options.DisableStringReuse.HasValue)
                    kestrelOpts.DisableStringReuse = options.DisableStringReuse.Value;
                if (options.ResponseHeaderEncodingSelector != null)
                    kestrelOpts.ResponseHeaderEncodingSelector = options.ResponseHeaderEncodingSelector;
                if (options.RequestHeaderEncodingSelector != null)
                    kestrelOpts.RequestHeaderEncodingSelector = options.RequestHeaderEncodingSelector;

                if (options.Limits.MaxRequestBodySize.HasValue)
                    kestrelOpts.Limits.MaxRequestBodySize = options.Limits.MaxRequestBodySize;
                if (options.Limits.MaxConcurrentConnections.HasValue)
                    kestrelOpts.Limits.MaxConcurrentConnections = options.Limits.MaxConcurrentConnections;
                if (options.Limits.MaxRequestHeaderCount > 0)
                    kestrelOpts.Limits.MaxRequestHeaderCount = options.Limits.MaxRequestHeaderCount;
                if (options.Limits.KeepAliveTimeout != default(TimeSpan))
                    kestrelOpts.Limits.KeepAliveTimeout = options.Limits.KeepAliveTimeout;



                // Optionally, handle ApplicationName or other properties as needed
                if (_listenerOptions != null && _listenerOptions.Count > 0)
                {
                    _listenerOptions.ForEach(opt =>
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

            // Build the WebApplication
            App = builder.Build();
            //  App.UsePowerShellRunspace(_runspacePool);
            App.UseLanguageRuntime(ScriptLanguage.PowerShell, branch => branch.UsePowerShellRunspace(_runspacePool));
            App.UseResponseCompression();
            _isConfigured = true;
        }

        #endregion
        #region Run/Start/Stop

        /// <summary>
        /// Runs the web application using the configured options.
        /// </summary>
        public void Run()
        {
            if (Log.IsEnabled(LogEventLevel.Debug))
                Log.Debug("Run() called");
            ApplyConfiguration();

            App?.Run();
        }

        /// <summary>
        /// Starts the web application without blocking the calling thread.
        /// </summary>
        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            if (Log.IsEnabled(LogEventLevel.Debug))
                Log.Debug("StartAsync() called");
            ApplyConfiguration();
            if (App != null)
            {
                await App.StartAsync(cancellationToken);
            }
        }

        /// <summary>
        /// Stops the web application gracefully.
        /// </summary>
        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            if (Log.IsEnabled(LogEventLevel.Debug))
                Log.Debug("StopAsync() called");
            if (App != null)
            {
                await App.StopAsync(cancellationToken);
            }
        }

        /// <summary>
        /// Requests the application to stop.
        /// </summary>
        public void Stop()
        {
            if (Log.IsEnabled(LogEventLevel.Debug))
                Log.Debug("Stop() called");
            // This initiates a graceful shutdown.
            App?.Lifetime.StopApplication();
        }

        #endregion



        #region Runspace Pool Management

        private static readonly object _poolGate = new();          // protects (_runspacePool, iss)

        /// <summary>
        /// Ensures _runspacePool is in an Opened state.
        /// If the pool is Broken/Closed it is torn down and recreated.
        /// Call this right before you create a PowerShell instance.
        /// </summary>
        private static void EnsureRunspacePoolOpen(RunspacePool? runspacePool)
        {
            if (Log.IsEnabled(LogEventLevel.Debug))
                Log.Debug("EnsureRunspacePoolOpen() called");
            if (runspacePool == null) { throw new ArgumentNullException(nameof(runspacePool), "Runspace pool cannot be null."); }
            if (runspacePool.RunspacePoolStateInfo.State == RunspacePoolState.Opened)
            {
                Log.Verbose("Runspace pool is already opened.");
                return;
            }
            Log.Verbose("Ensuring runspace pool is open...");
            lock (_poolGate)
            {

                var state = runspacePool.RunspacePoolStateInfo.State;
                switch (state)
                {
                    // brand-new pool
                    case RunspacePoolState.BeforeOpen:
                        Log.Verbose("Before opening runspace pool, opening now...");
                        runspacePool.Open();                      // blocks until Opened
                        break;

                    // another thread is already opening – wait
                    case RunspacePoolState.Opening:
                        Log.Verbose("Runspace pool is opening, waiting for it to complete...");
                        // Wait until the pool is opened
                        while (runspacePool.RunspacePoolStateInfo.State == RunspacePoolState.Opening)
                            Thread.Sleep(25);
                        break;

                    // pool closed or broken – throw it away and start fresh
                    case RunspacePoolState.Closed:
                    case RunspacePoolState.Broken:
                        Log.Warning($"Runspace pool is {state}, disposing and recreating...");
                        // Dispose the old pool and create a new one
                        runspacePool.Dispose();
                        // Recreate the runspace pool
                        //   Log.Verbose("Creating a new runspace pool...");
                        // Recreate the runspace pool with the specified max runspaces
                        // runspacePool = CreateRunspacePool(_kestrelOptions?.MaxRunspaces);
                        // runspacePool.Open();
                        throw new InvalidOperationException(
                             $"Runspace pool is {state}, cannot use it. A new pool must be created.");


                        // Opened / Disconnecting / Disconnected / Closing → leave alone,
                        // callers will handle Disconnecting/Closing by catching exceptions
                }
            }
        }


        private RunspacePool CreateRunspacePool(int? maxRunspaces = 0)
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
            foreach (var kvp in GlobalVariables.GetAllValues())
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
            var runspacePool = RunspaceFactory.CreateRunspacePool(initialSessionState: iss) ?? throw new InvalidOperationException("Failed to create runspace pool.");
            runspacePool.ThreadOptions = PSThreadOptions.ReuseThread;
            runspacePool.SetMinRunspaces(_kestrelOptions?.MinRunspaces ?? 1);
            runspacePool.SetMaxRunspaces(_kestrelOptions?.MaxRunspaces ?? Environment.ProcessorCount * 2);

            // Set the maximum number of runspaces to the specified value or default to 2x CPU cores
            _ = runspacePool.SetMaxRunspaces(maxRs);

            runspacePool.ApartmentState = ApartmentState.MTA;  // multi-threaded apartment

            Log.Information($"Runspace pool created with max runspaces: {maxRs}");

            // Return the created runspace pool
            return runspacePool;
        }


        #endregion


        #region Disposable

        /// <summary>
        /// Disposes the runspace pool and releases resources.
        /// </summary>
        public void Dispose()
        {
            if (Log.IsEnabled(LogEventLevel.Debug))
                Log.Debug("Dispose() called");
            _runspacePool?.Dispose();
            _runspacePool = null;
            _kestrelOptions = null;
            _listenerOptions?.Clear();
            App = null;
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
}
