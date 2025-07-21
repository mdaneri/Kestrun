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

    #endregion


    // Accepts optional module paths (from PowerShell)
    #region Constructor


    public KestrunHost(string? appName = null, string? kestrunRoot = null, string[]? modulePathsObj = null, Serilog.Core.Logger? logger = null)
    {
        // Initialize Serilog logger if not provided
        Log.Logger = logger ?? CreateDefaultLogger();
        if (Log.IsEnabled(LogEventLevel.Debug))
            Log.Debug("KestrunHost constructor called with appName: {AppName}, default logger: {DefaultLogger}, kestrunRoot: {KestrunRoot}, modulePathsObj length: {ModulePathsLength}", appName, logger == null, KestrunRoot, modulePathsObj?.Length ?? 0);
        if (!string.IsNullOrWhiteSpace(kestrunRoot))
        {
            Log.Information("Setting Kestrun root directory: {KestrunRoot}", kestrunRoot);
            Directory.SetCurrentDirectory(kestrunRoot);
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
    private static Serilog.Core.Logger CreateDefaultLogger()
        => new LoggerConfiguration()
            .MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
            .Enrich.FromLogContext()
            .WriteTo.File("logs/kestrun.log", rollingInterval: RollingInterval.Day)
            .CreateLogger();

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

    private List<ListenerOptions>? _listenerOptions;


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
                using PowerShell ps = PowerShell.Create(_pool.Acquire());

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
                //var psResults = await Task.Run(() => ps.Invoke())               // no pool dead-lock
               // .ConfigureAwait(false);
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



    public void ApplyConfiguration()
    {
        if (Log.IsEnabled(LogEventLevel.Debug))
            Log.Debug("ApplyConfiguration(options) called");

        if (_isConfigured)
        {
            if (Log.IsEnabled(LogEventLevel.Debug))
                Log.Debug("Configuration already applied, skipping");
            return; // Already configured
        }

        // This method is called to apply the configured options to the Kestrel server.
        // The actual application of options is done in the Run method.
        _runspacePool = CreateRunspacePool(Options.MaxRunspaces);
        if (_runspacePool == null)
        {
            throw new InvalidOperationException("Failed to create runspace pool.");
        }

        KestrelServices(builder);
        builder.WebHost.UseKestrel(opts =>
        {
            opts.CopyFromTemplate(Options.ServerOptions);
        });
        builder.WebHost.ConfigureKestrel(kestrelOpts =>
        {
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

        /*  App.UseStaticFiles(new StaticFileOptions
          {
              FileProvider = new PhysicalFileProvider(Path.Combine(KestrunRoot, "public")),
              RequestPath = "/assets",
              DefaultContentType = "text/plain",
              ServeUnknownFileTypes = true,
              OnPrepareResponse = ctx =>
                  {
                      var headers = ctx.Context.Response.Headers;

                      // Set a fixed or computed Last-Modified time
                      if (!string.IsNullOrEmpty(ctx.File.PhysicalPath))
                      {
                          var lastModified = File.GetLastWriteTimeUtc(ctx.File.PhysicalPath);
                          headers.LastModified = lastModified.ToUniversalTime().ToString("R"); // RFC1123 format
                      }
                  }
          });*/
        App.UseStaticFiles(); // Serve static files from wwwroot by default
                              //   App.UseDefaultFiles(); // Serve default files like index.html
        App.UseRouting();
        App.UseLanguageRuntime(ScriptLanguage.PowerShell, branch => branch.UsePowerShellRunspace(_runspacePool));
        App.UseResponseCompression();                // optional
        App.UsePowerShellRazorPages(_runspacePool!); // +++ PowerShell→Razor bridge
        App.MapRazorPages();                       // +++ route the .cshtml files


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
        _isConfigured = true;
    }

    #endregion
    #region Run/Start/Stop

    public void Run()
    {
        if (Log.IsEnabled(LogEventLevel.Debug))
            Log.Debug("Run() called");
        ApplyConfiguration();

        App?.Run();
    }

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
