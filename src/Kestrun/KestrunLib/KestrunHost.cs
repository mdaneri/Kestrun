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


namespace KestrelLib
{
    public class KestrunHost
    {
        private readonly ConcurrentDictionary<string, string> sharedState = new();
        private readonly WebApplicationBuilder builder;
        private WebApplication? App;

        private KestrunOptions? _kestrelOptions;
        private readonly List<string> _modulePaths = new();

        private bool _isConfigured = false;

        private RunspacePool? _runspacePool;


        // Accepts optional module paths (from PowerShell)
        public KestrunHost(string? appName = null, object? modulePathsObj = null)
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
                        _modulePaths.Add(modPath);
                    }
                }
            }
        }


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


        public void ConfigureKestrel(KestrunOptions options)
        {
            _kestrelOptions = options;
        }



        private List<ListenerOptions>? _listenerOptions;

        public void ConfigureListener(int port, IPAddress? iPAddress = null, string? certPath = null, string? certPassword = null, HttpProtocols protocols = HttpProtocols.Http1, bool useConnectionLogging = false)
        {
            if (_listenerOptions == null)
            {
                _listenerOptions = [];
            }
            _listenerOptions.Add(new ListenerOptions
            {
                IPAddress = iPAddress ?? IPAddress.Any,
                Port = port,
                UseHttps = !string.IsNullOrEmpty(certPath) && !string.IsNullOrEmpty(certPassword),
                CertPath = certPath,
                CertPassword = certPassword,
                Protocols = protocols,
                UseConnectionLogging = useConnectionLogging
            });

        }


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
        private RequestDelegate BuildPyDelegate(string code)
        {
            PythonEngine.Initialize();
            using var gil = Py.GIL();
            dynamic scope = Py.CreateScope();
            scope.Exec(code);                    // script defines:  def handle(ctx, res): ...

            dynamic pyHandle = scope.Get("handle");

            return async ctx =>
            {
                using var _ = Py.GIL();
                var res = new KestrunResponse();
                pyHandle(ctx.ToPython(), res.ToPython());

                if (!string.IsNullOrEmpty(res.RedirectUrl))
                    return;

                await res.ApplyTo(ctx.Response);
            };
        }


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


        private record CsGlobals(HttpContext Ctx, KestrunResponse Res);

        private RequestDelegate BuildCsDelegate(string code)
        {
            var opts = ScriptOptions.Default
                         .WithImports("System", "System.Linq", "Microsoft.AspNetCore.Http")
                         .WithReferences(typeof(HttpContext).Assembly, typeof(KestrunResponse).Assembly);

            var script = CSharpScript.Create(code, opts, typeof(CsGlobals));
            script.Compile();

            return async ctx =>
            {
                var res = new KestrunResponse();
                var state = await script.RunAsync(new CsGlobals(ctx, res));

                if (!string.IsNullOrEmpty(res.RedirectUrl))
                    return;

                await res.ApplyTo(ctx.Response);
            };
        }

        private RequestDelegate BuildPsDelegate(string code)
        {
            var modules = _modulePaths.ToArray();
            var pool = RunspaceFactory.CreateRunspacePool(1, Environment.ProcessorCount);
            pool.Open();

            return async (HttpContext context) =>
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

                var inputJson = JsonSerializer.Serialize(request);

                var KrResponse = new KestrunResponse();

                using PowerShell ps = PowerShell.Create();
                ps.RunspacePool = _runspacePool;

                // Always import stored modules
                foreach (var modPath in _modulePaths)
                {
                    if (!string.IsNullOrWhiteSpace(modPath))
                    {
                        ps.AddScript($"Import-Module -Name '{modPath.Replace("'", "''")}' -ErrorAction SilentlyContinue").Invoke();
                        ps.Commands.Clear();
                    }
                }
                ps.AddCommand("Set-Variable").AddParameter("Name", "Request").AddParameter("Value", request).AddParameter("Scope", "Script").AddStatement();
                ps.AddCommand("Set-Variable").AddParameter("Name", "Response").AddParameter("Value", KrResponse).AddParameter("Scope", "Script").AddStatement();
                ps.AddScript(code);

                var results = await Task.Run(() => ps.Invoke());

                // Capture errors and output from the runspace
                var errorOutput = ps.Streams.Error.Select(e => e.ToString()).ToList();
                var verboseOutput = ps.Streams.Verbose.Select(v => v.ToString()).ToList();
                var warningOutput = ps.Streams.Warning.Select(w => w.ToString()).ToList();
                var debugOutput = ps.Streams.Debug.Select(d => d.ToString()).ToList();
                var infoOutput = ps.Streams.Information.Select(i => i.ToString()).ToList();

                if (ps.HadErrors || errorOutput.Count > 0)
                {
                    context.Response.StatusCode = 500;
                    var errorMsg = $"❌[Error]\n\t" + string.Join("\n\t", errorOutput);
                    if (verboseOutput.Count > 0)
                        errorMsg += "\n💬[Verbose]\n\t" + string.Join("\n\t", verboseOutput);
                    if (warningOutput.Count > 0)
                        errorMsg += "\n⚠️[Warning]\n\t" + string.Join("\n\t", warningOutput);
                    if (debugOutput.Count > 0)
                        errorMsg += "\n🐞[Debug]\n\t" + string.Join("\n\t", debugOutput);
                    if (infoOutput.Count > 0)
                        errorMsg += "\nℹ️[Info]\n\t" + string.Join("\n\t", infoOutput);
                    Console.WriteLine(errorMsg);

                }


                // If redirect, nothing to return
                if (!string.IsNullOrEmpty(KrResponse.RedirectUrl))
                    return;
                await KrResponse.ApplyTo(context.Response);
                // Optionally, you could return output/verbose/debug info here for diagnostics
                // return string.Join("\n", results.Select(r => r.ToString()));
                return;
            };
        }



        public void AddRoute(string pattern,
                               string scriptBlock,
                               ScriptLanguage language = ScriptLanguage.PowerShell,
                               string httpMethod = "GET")
        {
            if (App is null)
                throw new InvalidOperationException(
                    "WebApplication is not initialized. Call ApplyConfiguration first.");

            // compile once – return an HttpContext->Task delegate
            var handler = language switch
            {
                ScriptLanguage.PowerShell => BuildPsDelegate(scriptBlock),
                ScriptLanguage.CSharp => BuildCsDelegate(scriptBlock),
                ScriptLanguage.FSharp => BuildFsDelegate(scriptBlock), // F# scripting not implemented
                ScriptLanguage.Python => BuildPyDelegate(scriptBlock),
                ScriptLanguage.JavaScript => BuildJsDelegate(scriptBlock),
                _ => throw new NotSupportedException(language.ToString())
            };

            App.MapMethods(pattern, new[] { httpMethod.ToUpperInvariant() }, handler);
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

                    var inputJson = JsonSerializer.Serialize(request);

                    var KrResponse = new KestrunResponse();

                    using PowerShell ps = PowerShell.Create();
                    ps.RunspacePool = _runspacePool; 

                    // Always import stored modules
                    foreach (var modPath in _modulePaths)
                    {
                        if (!string.IsNullOrWhiteSpace(modPath))
                        {
                            ps.AddScript($"Import-Module -Name '{modPath.Replace("'", "''")}' -ErrorAction SilentlyContinue").Invoke();
                            ps.Commands.Clear();
                        }
                    }
                    ps.AddCommand("Set-Variable").AddParameter("Name", "Request").AddParameter("Value", request).AddParameter("Scope", "Script").AddStatement();
                    ps.AddCommand("Set-Variable").AddParameter("Name", "Response").AddParameter("Value", KrResponse).AddParameter("Scope", "Script").AddStatement();
                    ps.AddScript(scriptBlock);

                    var results = await Task.Run(() => ps.Invoke());

                    // Capture errors and output from the runspace
                    var errorOutput = ps.Streams.Error.Select(e => e.ToString()).ToList();
                    var verboseOutput = ps.Streams.Verbose.Select(v => v.ToString()).ToList();
                    var warningOutput = ps.Streams.Warning.Select(w => w.ToString()).ToList();
                    var debugOutput = ps.Streams.Debug.Select(d => d.ToString()).ToList();
                    var infoOutput = ps.Streams.Information.Select(i => i.ToString()).ToList();

                    if (ps.HadErrors || errorOutput.Count > 0)
                    {
                        context.Response.StatusCode = 500;
                        var errorMsg = $"❌[Error]\n\t" + string.Join("\n\t", errorOutput);
                        if (verboseOutput.Count > 0)
                            errorMsg += "\n💬[Verbose]\n\t" + string.Join("\n\t", verboseOutput);
                        if (warningOutput.Count > 0)
                            errorMsg += "\n⚠️[Warning]\n\t" + string.Join("\n\t", warningOutput);
                        if (debugOutput.Count > 0)
                            errorMsg += "\n🐞[Debug]\n\t" + string.Join("\n\t", debugOutput);
                        if (infoOutput.Count > 0)
                            errorMsg += "\nℹ️[Info]\n\t" + string.Join("\n\t", infoOutput);
                        Console.WriteLine(errorMsg);
                        return errorMsg;
                    }


                    // If redirect, nothing to return
                    if (!string.IsNullOrEmpty(KrResponse.RedirectUrl))
                        return string.Empty;
                    await KrResponse.ApplyTo(context.Response);
                    // Optionally, you could return output/verbose/debug info here for diagnostics
                    // return string.Join("\n", results.Select(r => r.ToString()));
                    return string.Empty;
                });
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to add route '{pattern}' with method '{httpMethod}': {ex.Message}", ex);
            }
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
            /*    if (_kestrelOptions.Count == 0)
                {
                    throw new InvalidOperationException("No Kestrel options configured. Call ConfigureKestrel first.");
                }
    */
            // This method is called to apply the configured options to the Kestrel server.
            // The actual application of options is done in the Run method.

            _runspacePool = RunspaceFactory.CreateRunspacePool(1, options.MaxRunspaces ?? Environment.ProcessorCount);
            if (_runspacePool == null)
            {
                throw new InvalidOperationException("Failed to create runspace pool.");
            }
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
                            if (opt.UseHttps && !string.IsNullOrEmpty(opt.CertPath))
                                listenOptions.UseHttps(opt.CertPath, opt.CertPassword);
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
    }
}
