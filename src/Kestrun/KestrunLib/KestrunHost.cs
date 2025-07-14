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


namespace KestrumLib
{
    public class KestrunHost
    {
        #region Fields
        // Shared state across routes
        private readonly ConcurrentDictionary<string, string> sharedState = new();
        private readonly WebApplicationBuilder builder;
        private WebApplication? App;

        private KestrunOptions? _kestrelOptions;
        private readonly List<string> _modulePaths = new();

        private bool _isConfigured = false;

        private RunspacePool? _runspacePool;
        #endregion


        // Accepts optional module paths (from PowerShell)
        #region Constructor
        public KestrunHost(string? appName = null, string[]? modulePathsObj = null)
        {
            builder = WebApplication.CreateBuilder();
            if (!string.IsNullOrEmpty(appName))
            {
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
                            _modulePaths.Add(modPath);
                        }
                        else
                        {
                            Console.WriteLine($"[KestrunHost] Warning: Module path does not exist: {modPath}");
                        }
                    }
                    else
                    {
                        Console.WriteLine("[KestrunHost] Warning: Invalid module path provided.");
                    }
                }
            }
        }
        #endregion


        private void KestrelServices(WebApplicationBuilder builder)
        {
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


        public void ConfigureListener(int port, IPAddress? ipAddress = null, X509Certificate2? x509Certificate = null, HttpProtocols protocols = HttpProtocols.Http1, bool useConnectionLogging = false)
        {
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
            var engine = new V8ScriptEngine();
            engine.AddHostType("KestrunResponse", typeof(KestrunResponse));
            engine.Execute(code);               // script defines global  function handle(ctx, res) { ... }

            return async ctx =>
            {
                var res = new KestrunResponse();
                engine.Script.handle(ctx, res);

                if (!string.IsNullOrEmpty(res.RedirectUrl))
                    return;

                await res.ApplyTo(ctx.Response);
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
            return async ctx =>
            {
                try
                {
                    using var _ = Py.GIL();       // enter GIL for *this* request

                    var res = new KestrunResponse();

                    // Call the Python handler (Python → .NET marshal is automatic)
                    pyHandle(ctx, res);

                    // redirect?
                    if (!string.IsNullOrEmpty(res.RedirectUrl))
                    {
                        ctx.Response.Redirect(res.RedirectUrl);
                        return;                   // finally-block will CompleteAsync
                    }

                    // normal response
                    await res.ApplyTo(ctx.Response).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    // optional logging
                    Console.WriteLine($"Python route error: {ex}");
                    ctx.Response.StatusCode = 500;
                    ctx.Response.ContentType = "text/plain; charset=utf-8";
                    await ctx.Response.WriteAsync(
                        "Python script failed while processing the request.").ConfigureAwait(false);
                }
                finally
                {
                    // Always flush & close so the client doesn’t hang
                    try { await ctx.Response.CompleteAsync().ConfigureAwait(false); }
                    catch (ObjectDisposedException) { /* client disconnected */ }
                }
            };
        }



        #endregion


        private RequestDelegate BuildFsDelegate(string code)
        {
#pragma warning disable CS0618  // LegacyReferenceResolver is obsolete
            var fsi = Shell.FsiEvaluationSession.Create(
                       Shell.FsiEvaluationSession.GetDefaultConfiguration(),
                        ["fsi.exe", "--noninteractive"],
                          inReader: Console.In,
                          outWriter: Console.Out,
                          errorWriter: Console.Error,
                          collectible: FSharpOption<bool>.None,
                          legacyReferenceResolver: FSharpOption<FSharp.Compiler.CodeAnalysis.LegacyReferenceResolver?>.None
            );
#pragma warning restore CS0618  // LegacyReferenceResolver is obsolete

            // script must define:  let handle (ctx: HttpContext) (res: KestrunResponse) = ...
            fsi.EvalInteraction(code, null);

            return async ctx =>
            {
                var res = new KestrunResponse();
                fsi.AddBoundValue("ctx", ctx);
                fsi.AddBoundValue("res", res);
                fsi.EvalInteraction("handle ctx res", null);

                if (!string.IsNullOrEmpty(res.RedirectUrl))
                    return;

                await res.ApplyTo(ctx.Response);
            };
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
            // 1. Compose ScriptOptions
            var opts = ScriptOptions.Default
                       .WithImports("System", "System.Linq", "System.Threading.Tasks", "Microsoft.AspNetCore.Http")
                       .WithReferences(typeof(HttpContext).Assembly, typeof(KestrunResponse).Assembly)
                       .WithLanguageVersion(languageVersion);

            if (extraImports is { Length: > 0 })
                opts = opts.WithImports(opts.Imports.Concat(extraImports));

            if (extraRefs is { Length: > 0 })
                opts = opts.WithReferences(opts.MetadataReferences
                                              .Concat(extraRefs.Select(r => MetadataReference.CreateFromFile(r.Location))));

            // 2. Compile once
            var script = CSharpScript.Create(code, opts, typeof(CsGlobals));
            var diagnostics = script.Compile();
            if (diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error))
                throw new CompilationErrorException("C# route code has errors:", diagnostics);

            // 3. Build the per-request delegate
            return async context =>
            {
                try
                {
                    var krRequest = await KestrunRequest.NewRequest(context);
                    var krResponse = new KestrunResponse();
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
        private RequestDelegate BuildPsDelegate(string code)
        {

            return async context =>
            {
                try
                {
                    var krRequest = await KestrunRequest.NewRequest(context);
                    var krResponse = new KestrunResponse();

                    EnsureRunspacePoolOpen();
                    using PowerShell ps = PowerShell.Create();
                    ps.RunspacePool = _runspacePool;


                    ps.AddCommand("Set-Variable").AddParameter("Name", "Request").AddParameter("Value", krRequest).AddParameter("Scope", "Script").AddStatement().
                     AddCommand("Set-Variable").AddParameter("Name", "Response").AddParameter("Value", krResponse).AddParameter("Scope", "Script").AddStatement().
                     AddScript(code);
                    // Execute the PowerShell script block
                    // Using Task.Run to avoid blocking the thread 
                    Console.WriteLine("Executing PowerShell script...");
                    // Using Task.Run to avoid blocking the thread
                    // This is necessary to prevent deadlocks in the runspace pool
                    var psResults = await Task.Run(() => ps.Invoke())               // no pool dead-lock
                    .ConfigureAwait(false);

                    Console.WriteLine($"PowerShell script executed with {psResults.Count} results.");
                    //  var psResults = await Task.Run(() => ps.Invoke());
                    // Capture errors and output from the runspace 
                    if (ps.HadErrors || ps.Streams.Error.Count != 0)
                    {
                        await BuildError.ResponseAsync(context, ps);
                        return;
                    }

                    Console.WriteLine("PowerShell script completed successfully.");
                    // If redirect, nothing to return
                    if (!string.IsNullOrEmpty(krResponse.RedirectUrl))
                    {
                        Console.WriteLine($"Redirecting to {krResponse.RedirectUrl}");
                        context.Response.Redirect(krResponse.RedirectUrl);
                        return;
                    }
                    Console.WriteLine("Applying response to HttpResponse...");
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
                    Console.WriteLine($"Error processing request: {ex.Message}");
                    context.Response.StatusCode = 500; // Internal Server Error
                    context.Response.ContentType = "text/plain; charset=utf-8";
                    await context.Response.WriteAsync("An error occurred while processing your request.");
                }
                finally
                {
                    // CompleteAsync is idempotent – safe to call once more
                    try
                    {
                        Console.WriteLine("Completing response for " + context.Request.Path);
                        await context.Response.CompleteAsync().ConfigureAwait(false);
                    }
                    catch (ObjectDisposedException)
                    {
                        // response has already been torn down (e.g., client aborted)
                    }
                }
            };
        }
        #endregion


        #region Route


        public void AddRoute(string pattern,
                                    string scriptBlock,
                                    ScriptLanguage language = ScriptLanguage.PowerShell,
                                    string httpMethod = "GET",
                                    string[]? extraImports = null,
                                    Assembly[]? extraRefs = null)
        {
            if (App is null)
                throw new InvalidOperationException(
                    "WebApplication is not initialized. Call ApplyConfiguration first.");

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

            App.MapMethods(pattern, [httpMethod.ToUpperInvariant()], handler);
        }



        public void AddRoute(string pattern, string scriptBlock, string httpMethod = "GET")
        {
            try
            {
                if (App == null)
                {
                    throw new InvalidOperationException("WebApplication is not initialized. Call ApplyConfiguration first.");
                }
                _ = App.MapMethods(pattern, [httpMethod.ToUpperInvariant()], async (HttpContext context) =>
                {
                    using var reader = new StreamReader(context.Request.Body);
                    var body = await reader.ReadToEndAsync();
                    // context.Request.Headers.Remove("Accept-Encoding");
                    var request = new
                    {
                        context.Request.Method,
                        Path = context.Request.Path.ToString(),
                        Query = context.Request.Query.ToDictionary(x => x.Key, x => x.Value.ToString()),
                        Headers = context.Request.Headers.ToDictionary(x => x.Key, x => x.Value.ToString()),
                        Body = body
                    };

                    var inputJson = JsonConvert.SerializeObject(request);

                    var krResponse = new KestrunResponse();

                    using PowerShell ps = PowerShell.Create();
                    ps.RunspacePool = _runspacePool;


                    ps.AddCommand("Set-Variable").AddParameter("Name", "Request").AddParameter("Value", request).AddParameter("Scope", "Script").AddStatement().
                     AddCommand("Set-Variable").AddParameter("Name", "Response").AddParameter("Value", krResponse).AddParameter("Scope", "Script").AddStatement().
                     AddScript(scriptBlock);
                    // Execute the PowerShell script block
                    // Using Task.Run to avoid blocking the thread 
                    var psResults = await ps.InvokeAsync();
                    // Capture errors and output from the runspace
                    var hadErrors = ps.HadErrors || ps.Streams.Error.Count != 0;
                    if (hadErrors)
                    {
                        return BuildError.Result(ps);
                    }


                    // If redirect, nothing to return
                    if (!string.IsNullOrEmpty(krResponse.RedirectUrl))
                    {
                        context.Response.Redirect(krResponse.RedirectUrl);
                        return Results.Redirect(krResponse.RedirectUrl);
                    }
                    await krResponse.ApplyTo(context.Response);
                    return Results.Empty;
                });
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to add route '{pattern}' with method '{httpMethod}': {ex.Message}", ex);
            }
        }


        #endregion
        #region Configuration
        public void ConfigureKestrel(KestrunOptions options)
        {
            _kestrelOptions = options;
        }

        public void ApplyConfiguration()
        {
            if (_kestrelOptions == null)
            {
                _kestrelOptions = new KestrunOptions();
            }
            ApplyConfiguration(_kestrelOptions);
        }

        public void ApplyConfiguration(KestrunOptions options)
        {
            if (_isConfigured)
            {
                return; // Already configured
            }

            // This method is called to apply the configured options to the Kestrel server.
            // The actual application of options is done in the Run method.
            _runspacePool = CreateRunspacePool(options.MaxRunspaces);

            _runspacePool?.Open();
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
            App = builder.Build();
            App.UseResponseCompression();
            _isConfigured = true;
        }

        #endregion
        #region Run/Start/Stop

        public void Run()
        {
            ApplyConfiguration();

            App?.Run();
        }

        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            ApplyConfiguration();
            if (App != null)
            {
                await App.StartAsync(cancellationToken);
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            if (App != null)
            {
                await App.StopAsync(cancellationToken);
            }
        }

        public void Stop()
        {
            // This initiates a graceful shutdown.
            App?.Lifetime.StopApplication();
        }

        #endregion



        #region Runspace Pool Management

        private readonly object _poolGate = new();          // protects (_runspacePool, iss)

        /// <summary>
        /// Ensures _runspacePool is in an Opened state.
        /// If the pool is Broken/Closed it is torn down and recreated.
        /// Call this right before you create a PowerShell instance.
        /// </summary>
        private void EnsureRunspacePoolOpen()
        {
            // Fast-path: already opened
            if (_runspacePool?.RunspacePoolStateInfo.State == RunspacePoolState.Opened)
            {
                Console.WriteLine("Runspace pool is already opened.");
                return;
            }
            Console.WriteLine("Ensuring runspace pool is open...");
            lock (_poolGate)
            {
                // Pool was never created
                _runspacePool ??= CreateRunspacePool(_kestrelOptions?.MaxRunspaces);

                var state = _runspacePool.RunspacePoolStateInfo.State;
                switch (state)
                {
                    // brand-new pool
                    case RunspacePoolState.BeforeOpen:
                        Console.WriteLine("Before opening runspace pool, opening now...");
                        _runspacePool.Open();                      // blocks until Opened
                        break;

                    // another thread is already opening – wait
                    case RunspacePoolState.Opening:
                        Console.WriteLine("Runspace pool is opening, waiting for it to complete...");
                        // Wait until the pool is opened
                        while (_runspacePool.RunspacePoolStateInfo.State == RunspacePoolState.Opening)
                            Thread.Sleep(25);
                        break;

                    // pool closed or broken – throw it away and start fresh
                    case RunspacePoolState.Closed:
                    case RunspacePoolState.Broken:
                        Console.WriteLine($"Runspace pool is {state}, disposing and recreating...");
                        // Dispose the old pool and create a new one
                        _runspacePool.Dispose();
                        // Recreate the runspace pool
                        Console.WriteLine("Creating a new runspace pool...");
                        // Recreate the runspace pool with the specified max runspaces
                        _runspacePool = CreateRunspacePool(_kestrelOptions?.MaxRunspaces);
                        _runspacePool.Open();
                        break;

                        // Opened / Disconnecting / Disconnected / Closing → leave alone,
                        // callers will handle Disconnecting/Closing by catching exceptions
                }
            }
        }


        private RunspacePool CreateRunspacePool(int? maxRunspaces = 0)
        {
            // Create a default InitialSessionState _with_ an unrestricted policy:
            var iss = InitialSessionState.CreateDefault();

            iss.ExecutionPolicy = Microsoft.PowerShell.ExecutionPolicy.Unrestricted;

            foreach (var p in _modulePaths)
            {
                iss.ImportPSModule([p]);
            }
            int maxRs = (maxRunspaces.HasValue && maxRunspaces.Value > 0) ? maxRunspaces.Value : Environment.ProcessorCount * 2;

            Console.WriteLine($"Creating runspace pool with max runspaces: {maxRs}");
            var runspacePool = RunspaceFactory.CreateRunspacePool(initialSessionState: iss) ?? throw new InvalidOperationException("Failed to create runspace pool.");
            runspacePool.ThreadOptions = PSThreadOptions.ReuseThread;
            runspacePool.SetMinRunspaces(1);
            runspacePool.SetMaxRunspaces(_kestrelOptions?.MaxRunspaces ?? Environment.ProcessorCount * 2);

            // Set the maximum number of runspaces to the specified value or default to 2x CPU cores
            _ = runspacePool.SetMaxRunspaces(maxRs);

            runspacePool.ApartmentState = ApartmentState.MTA;  // multi-threaded apartment

            Console.WriteLine($"Runspace pool created with max runspaces: {maxRs}");

            // Return the created runspace pool
            return runspacePool;
        }


        #endregion


        #region Disposable

        public void Dispose()
        {
            _runspacePool?.Dispose();
            _runspacePool = null;
            _kestrelOptions = null;
            _listenerOptions?.Clear();
            App = null;
        }
        #endregion
    }
}
