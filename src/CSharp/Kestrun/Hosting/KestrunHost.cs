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
using Kestrun.Utilities;
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
using Kestrun.SharedState;
using Kestrun.Languages;
using static Kestrun.Languages.CSharpDelegateBuilder;
using Kestrun.Middleware;
using Kestrun.Razor;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Quic;
using Kestrun.Scripting;
using Kestrun.Hosting;
/*#if NET8_0_OR_GREATER
[assembly: System.Runtime.Versioning.RequiresPreviewFeatures]
#endif
*/
namespace Kestrun;

public class KestrunHost : IDisposable
{
    #region Fields

    // Shared state across routes
    private readonly ConcurrentDictionary<string, string> sharedState = new();
    private readonly WebApplicationBuilder builder;
    private WebApplication? App;

    public string ApplicationName => Options.ApplicationName ?? "KestrunApp";

    public KestrunOptions Options { get; private set; }
    private readonly List<string> _modulePaths = [];

    private bool _isConfigured = false;

    private KestrunRunspacePoolManager? _runspacePool;
    public string? KestrunRoot { get; private set; }

    public Serilog.ILogger _Logger { get; private set; }
    public SchedulerService Scheduler { get; internal set; } = null!; // Initialized in ConfigureServices

    // ── ✦ QUEUE #1 : SERVICE REGISTRATION ✦ ─────────────────────────────
    private readonly List<Action<IServiceCollection>> _serviceQueue = [];

    // ── ✦ QUEUE #2 : MIDDLEWARE STAGES ✦ ────────────────────────────────
    private readonly List<Action<IApplicationBuilder>> _middlewareQueue = [];

    private readonly List<Action<KestrunHost>> _featureQueue = [];
    #endregion


    // Accepts optional module paths (from PowerShell)
    #region Constructor

    public KestrunHost(string? appName, string? kestrunRoot = null, string[]? modulePathsObj = null) :
            this(appName, Log.Logger, kestrunRoot, modulePathsObj)
    { }

    public KestrunHost(string? appName, Serilog.ILogger logger, string? kestrunRoot = null, string[]? modulePathsObj = null)
    {
        // Initialize Serilog logger if not provided
        _Logger = logger ?? Log.Logger;

        if (_Logger.IsEnabled(LogEventLevel.Debug))
            _Logger.Debug("KestrunHost constructor called with appName: {AppName}, default logger: {DefaultLogger}, kestrunRoot: {KestrunRoot}, modulePathsObj length: {ModulePathsLength}", appName, logger == null, KestrunRoot, modulePathsObj?.Length ?? 0);
        if (!string.IsNullOrWhiteSpace(kestrunRoot))
        {
            if (Directory.GetCurrentDirectory() != kestrunRoot)
            {
                Directory.SetCurrentDirectory(kestrunRoot);
                _Logger.Information("Changed current directory to Kestrun root: {KestrunRoot}", kestrunRoot);
            }
            else
            {
                _Logger.Verbose("Current directory is already set to Kestrun root: {KestrunRoot}", kestrunRoot);
            }
            KestrunRoot = kestrunRoot;
        }
        var kestrunModulePath = string.Empty;
        if (modulePathsObj is null || (modulePathsObj?.Any(p => p.Contains("Kestrun.psm1", StringComparison.Ordinal)) == false))
        {
            kestrunModulePath = PowerShellModuleLocator.LocateKestrunModule();
            if (string.IsNullOrWhiteSpace(kestrunModulePath))
            {
                _Logger.Fatal("Kestrun module not found. Ensure the Kestrun module is installed.");
                throw new FileNotFoundException("Kestrun module not found.");
            }

            _Logger.Information("Found Kestrun module at: {KestrunModulePath}", kestrunModulePath);
            _Logger.Verbose("Adding Kestrun module path: {KestrunModulePath}", kestrunModulePath);
            _modulePaths.Add(kestrunModulePath);
        }


        builder = WebApplication.CreateBuilder();
        // Add Serilog to ASP.NET Core logging
        builder.Host.UseSerilog();
        if (string.IsNullOrEmpty(appName))
        {
            _Logger.Information("No application name provided, using default.");
            Options = new KestrunOptions();
        }
        else
        {
            _Logger.Information("Setting application name: {AppName}", appName);
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
                        _Logger.Information("[KestrunHost] Adding module path: {ModPath}", modPath);
                        _modulePaths.Add(modPath);
                    }
                    else
                    {
                        _Logger.Warning("[KestrunHost] Module path does not exist: {ModPath}", modPath);
                    }
                }
                else
                {
                    _Logger.Warning("[KestrunHost] Invalid module path provided.");
                }
            }
        }

        _Logger.Information("Current working directory: {CurrentDirectory}", Directory.GetCurrentDirectory());
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


    #region ListenerOptions 

    public KestrunHost ConfigureListener(int port, IPAddress? ipAddress = null, X509Certificate2? x509Certificate = null, HttpProtocols protocols = HttpProtocols.Http1, bool useConnectionLogging = false)
    {
        if (_Logger.IsEnabled(LogEventLevel.Debug))
            _Logger.Debug("ConfigureListener port={Port}, ipAddress={IPAddress}, protocols={Protocols}, useConnectionLogging={UseConnectionLogging}, certificate supplied={HasCert}", port, ipAddress, protocols, useConnectionLogging, x509Certificate != null);

        if (protocols == HttpProtocols.Http1AndHttp2AndHttp3 && !CcUtilities.PreviewFeaturesEnabled())
        {
            _Logger.Warning("Http3 is not supported in this version of Kestrun. Using Http1 and Http2 only.");
            protocols = HttpProtocols.Http1AndHttp2;
        }

        Options.Listeners.Add(new ListenerOptions
        {
            IPAddress = ipAddress ?? IPAddress.Any,
            Port = port,
            UseHttps = x509Certificate != null,
            X509Certificate = x509Certificate,
            Protocols = protocols,
            UseConnectionLogging = useConnectionLogging
        });
        return this;

    }

    public void ConfigureListener(int port, IPAddress? ipAddress = null, bool useConnectionLogging = false)
    {
        ConfigureListener(port: port, ipAddress: ipAddress, x509Certificate: null, protocols: HttpProtocols.Http1, useConnectionLogging: useConnectionLogging);
    }

    #endregion




    #region C#


    #endregion



    #region Route
    public delegate Task KestrunHandler(KestrunContext Context);

    public void AddNativeRoute(string pattern, HttpVerb httpVerb, KestrunHandler handler, string[]? requireAuthorization = null)
    {
        if (_Logger.IsEnabled(LogEventLevel.Debug))
            _Logger.Debug("AddNativeRoute called with pattern={Pattern}, httpVerb={HttpVerb}", pattern, httpVerb);
        AddNativeRoute(pattern: pattern, httpVerbs: [httpVerb], handler: handler, requireAuthorization: requireAuthorization);
    }

    public void AddNativeRoute(string pattern, IEnumerable<HttpVerb> httpVerbs, KestrunHandler handler, string[]? requireAuthorization = null)
    {
        if (_Logger.IsEnabled(LogEventLevel.Debug))
            _Logger.Debug("AddNativeRoute called with pattern={Pattern}, httpVerbs={HttpVerbs}", pattern, string.Join(", ", httpVerbs));

        AddNativeRoute(new MapRouteOptions
        {
            Pattern = pattern,
            HttpVerbs = httpVerbs,
            Language = ScriptLanguage.Native,
            RequireAuthorization = requireAuthorization ?? [] // No authorization by default
        }, handler);

    }

    public void AddNativeRoute(MapRouteOptions options, KestrunHandler handler)
    {
        if (_Logger.IsEnabled(LogEventLevel.Debug))
            _Logger.Debug("AddNativeRoute called with pattern={Pattern}, method={Methods}", options.Pattern, string.Join(", ", options.HttpVerbs));
        // Ensure the WebApplication is initialized
        if (App is null)
            throw new InvalidOperationException("WebApplication is not initialized. Call EnableConfiguration first.");

        // Validate options
        if (string.IsNullOrWhiteSpace(options.Pattern))
            throw new ArgumentException("Pattern cannot be null or empty.", nameof(options.Pattern));

        string[] methods = [.. options.HttpVerbs.Select(v => v.ToMethodString())];
        var map = App.MapMethods(options.Pattern, methods, async context =>
           {
               var req = await KestrunRequest.NewRequest(context);
               var res = new KestrunResponse(req);
               KestrunContext kestrunContext = new(req, res, context);
               await handler(kestrunContext);
               await res.ApplyTo(context.Response);
           });

        AddMapOptions(map, options);

        _Logger.Information("Added native route: {Pattern} with methods: {Methods}", options.Pattern, string.Join(", ", methods));
        // Add to the feature queue for later processing
        _featureQueue.Add(host => host.AddMapRoute(options));
    }


    public void AddMapRoute(string pattern, HttpVerb httpVerbs, string scriptBlock, ScriptLanguage language = ScriptLanguage.PowerShell,
                                     string[]? requireAuthorization = null)
    {
        AddMapRoute(new Hosting.MapRouteOptions
        {
            Pattern = pattern,
            HttpVerbs = [httpVerbs],
            Code = scriptBlock,
            Language = language,
            RequireAuthorization = requireAuthorization ?? [] // No authorization by default
        });

    }

    public void AddMapRoute(string pattern,
                                 IEnumerable<HttpVerb> httpVerbs,
                                 string scriptBlock,
                                 ScriptLanguage language = ScriptLanguage.PowerShell,
                                     string[]? requireAuthorization = null)
    {
        AddMapRoute(new MapRouteOptions
        {
            Pattern = pattern,
            HttpVerbs = httpVerbs,
            Code = scriptBlock,
            Language = language,
            RequireAuthorization = requireAuthorization ?? [] // No authorization by default
        });
    }
    public void AddMapRoute(MapRouteOptions options)
    {
        if (_Logger.IsEnabled(LogEventLevel.Debug))
            _Logger.Debug("AddMapRoute called with pattern={Pattern}, language={Language}, method={Methods}", options.Pattern, options.Language, options.HttpVerbs);

        // Ensure the WebApplication is initialized
        if (App is null)
            throw new InvalidOperationException("WebApplication is not initialized. Call EnableConfiguration first.");

        // Validate options
        if (string.IsNullOrWhiteSpace(options.Pattern))
            throw new ArgumentException("Pattern cannot be null or empty.", nameof(options.Pattern));

        // Validate code
        if (string.IsNullOrWhiteSpace(options.Code))
            throw new ArgumentException("ScriptBlock cannot be null or empty.", nameof(options.Code));
        var routeOptions = options;
        if (!options.HttpVerbs.Any())
        {
            // Create a new RouteOptions with HttpVerbs set to [HttpVerb.Get]
            routeOptions = options with { HttpVerbs = [HttpVerb.Get] };
        }
        try
        {
            var logger = Log.Logger.ForContext("Route", routeOptions.Pattern);
            // compile once – return an HttpContext->Task delegate
            var handler = options.Language switch
            {

                ScriptLanguage.PowerShell => PowerShellDelegateBuilder.Build(options.Code, logger),
                ScriptLanguage.CSharp => CSharpDelegateBuilder.Build(options.Code, logger, options.ExtraImports, options.ExtraRefs),
                ScriptLanguage.FSharp => FSharpDelegateBuilder.Build(options.Code, logger), // F# scripting not implemented
                ScriptLanguage.Python => PyDelegateBuilder.Build(options.Code, logger),
                ScriptLanguage.JavaScript => JScriptDelegateBuilder.Build(options.Code, logger),
                _ => throw new NotSupportedException(options.Language.ToString())
            };
            string[] methods = [.. options.HttpVerbs.Select(v => v.ToMethodString())];
            var map = App.MapMethods(options.Pattern, methods, handler).WithLanguage(options.Language);
            if (_Logger.IsEnabled(LogEventLevel.Debug))
                _Logger.Debug("Mapped route: {Pattern} with methods: {Methods}", options.Pattern, string.Join(", ", methods));

            AddMapOptions(map, options);

            _Logger.Information("Added route: {Pattern} with methods: {Methods}", options.Pattern, string.Join(", ", methods));
            // Add to the feature queue for later processing

        }
        catch (CompilationErrorException ex)
        {
            // Log the detailed compilation errors
            _Logger.Error($"Failed to add route '{options.Pattern}' due to compilation errors:");
            _Logger.Error(ex.GetDetailedErrorMessage());

            // Re-throw with additional context
            throw new InvalidOperationException(
                $"Failed to compile {options.Language} script for route '{options.Pattern}'. {ex.GetErrors().Count()} error(s) found.",
                ex);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to add route '{options.Pattern}' with method '{string.Join(", ", options.HttpVerbs)}' using {options.Language}: {ex.Message}",
                ex);
        }
    }


    private void AddMapOptions(IEndpointConventionBuilder map, MapRouteOptions options)
    {
        if (options.ShortCircuit)
        {
            _Logger.Verbose("Short-circuiting route: {Pattern} with status code: {StatusCode}", options.Pattern, options.ShortCircuitStatusCode);
            if (options.ShortCircuitStatusCode is null)
                throw new ArgumentException("ShortCircuitStatusCode must be set if ShortCircuit is true.", nameof(options.ShortCircuitStatusCode));
            map.ShortCircuit(options.ShortCircuitStatusCode);
        }

        if (options.AllowAnonymous) // Allow anonymous access to this route
        {
            _Logger.Verbose("Allowing anonymous access for route: {Pattern}", options.Pattern);
            map.AllowAnonymous();
        }
        else
        {
            _Logger.Debug("No anonymous access allowed for route: {Pattern}", options.Pattern);
        }

        if (options.DisableAntiforgery) // Disable CSRF protection for this route
        {
            map.DisableAntiforgery(); // Disable CSRF protection for this route
            _Logger.Verbose("CSRF protection disabled for route: {Pattern}", options.Pattern);
        }

        if (!string.IsNullOrWhiteSpace(options.RateLimitPolicyName)) // Apply rate limiting policy if specified
        {
            _Logger.Verbose("Applying rate limit policy: {RateLimitPolicyName} to route: {Pattern}", options.RateLimitPolicyName, options.Pattern);
            // Ensure RateLimiting is configured in the app
            map.RequireRateLimiting(options.RateLimitPolicyName);
        }
        if (options.RequireAuthorization is { Length: > 0 })
        {
            _Logger.Verbose("Requiring authorization for route: {Pattern} with policies: {Policies}", options.Pattern, string.Join(", ", options.RequireAuthorization));
            map.RequireAuthorization(new AuthorizeAttribute
            {
                AuthenticationSchemes = string.Join(',', options.RequireAuthorization)
            });
        }
        else
        {
            _Logger.Debug("No authorization required for route: {Pattern}", options.Pattern);
        }

        if (!string.IsNullOrWhiteSpace(options.CorsPolicyName)) // Apply CORS policy if specified
        {
            _Logger.Verbose("Applying CORS policy: {CorsPolicyName} to route: {Pattern}", options.CorsPolicyName, options.Pattern);
            // Ensure CORS is configured in the app
            // apply the route-specific policy
            map.RequireCors(options.CorsPolicyName);
        }
        else
        {
            _Logger.Debug("No CORS policy applied for route: {Pattern}", options.Pattern);
        }


        if (!string.IsNullOrEmpty(options.OpenAPI.OperationId))
        {
            _Logger.Verbose("Adding OpenAPI metadata for route: {Pattern} with OperationId: {OperationId}", options.Pattern, options.OpenAPI.OperationId);
            // Add OpenAPI metadata if specified
            map.WithName(options.OpenAPI.OperationId);
        }

        if (!string.IsNullOrWhiteSpace(options.OpenAPI.Summary))
        {
            _Logger.Verbose("Adding OpenAPI summary for route: {Pattern} with Summary: {Summary}", options.Pattern, options.OpenAPI.Summary);
            map.WithSummary(options.OpenAPI.Summary);
        }

        if (!string.IsNullOrWhiteSpace(options.OpenAPI.Description))
        {
            _Logger.Verbose("Adding OpenAPI description for route: {Pattern} with Description: {Description}", options.Pattern, options.OpenAPI.Description);
            map.WithDescription(options.OpenAPI.Description);
        }
        if (options.OpenAPI.Tags.Length > 0)
        {
            _Logger.Verbose("Adding OpenAPI tags for route: {Pattern} with Tags: {Tags}", options.Pattern, string.Join(", ", options.OpenAPI.Tags));
            map.WithTags(options.OpenAPI.Tags);
        }


        if (!string.IsNullOrWhiteSpace(options.OpenAPI.GroupName))
        {
            _Logger.Verbose("Adding OpenAPI group name for route: {Pattern} with GroupName: {GroupName}", options.Pattern, options.OpenAPI.GroupName);
            map.WithGroupName(options.OpenAPI.GroupName);
        }
    }
    public KestrunHost AddHtmlTemplateRoute(string pattern, string htmlFilePath)
    {
        // ① Read the file once at startup (avoid disk I/O per request)
        var template = File.ReadAllText(htmlFilePath);

        AddNativeRoute(pattern, HttpVerb.Get, async (ctx) =>
        {
            // ② Build your variables map
            var vars = new Dictionary<string, object?>()
            {
                ["Request.Path"] = ctx.Request.Path,
                ["Request.Method"] = ctx.Request.Method,
                ["QueryString"] = ctx.Request.Query.ToDictionary(q => q.Key, q => q.Value.ToString()),
                ["Form"] = ctx.Request.Form,
                ["Cookies"] = ctx.Request.Cookies,
                ["Headers"] = ctx.Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString()),
                ["UserAgent"] = ctx.Request.Headers["User-Agent"].ToString(),
                ["ServerSoftware"] = "Kestrun/" + Options.ApplicationName,
                ["ServerVersion"] = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown",
                ["ServerOS"] = Environment.OSVersion.ToString(),
                ["ServerArch"] = Environment.Is64BitOperatingSystem ? "x64" : "x86",
                ["ServerIP"] = ctx.HttpContext.Connection.LocalIpAddress?.ToString() ?? "unknown",
                ["ServerPort"] = ctx.HttpContext.Connection.LocalPort,
                ["ServerName"] = Environment.MachineName,
                ["Timestamp"] = DateTimeOffset.UtcNow.ToString("O")
            };
            // merge shared state
            foreach (var kv in SharedStateStore.Snapshot())
                vars[kv.Key] = kv.Value;

            // ③ Render in one pass
            var html = HtmlTemplateHelper.RenderInlineTemplate(template, vars);

            // ④ Send it
            await ctx.Response.WriteTextResponseAsync(html, 200, "text/html");
        });
        return this;
    }

    #endregion
    #region Configuration


    public void EnableConfiguration()
    {
        if (_Logger.IsEnabled(LogEventLevel.Debug))
            _Logger.Debug("EnableConfiguration(options) called");

        if (_isConfigured)
        {
            if (_Logger.IsEnabled(LogEventLevel.Debug))
                _Logger.Debug("Configuration already applied, skipping");
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
                _Logger.Warning("EndpointDataSource is empty. No endpoints configured.");
            }
            else
            {
                foreach (var ep in dataSource.Endpoints)
                {
                    _Logger.Information("➡️  Endpoint: {DisplayName}", ep.DisplayName);
                }
            }

            _isConfigured = true;
            _Logger.Information("Configuration applied successfully.");
        }
        catch (Exception ex)
        {
            _Logger.Error(ex, "Error applying configuration: {Message}", ex.Message);
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

        _Logger.Information("CWD: {CWD}", Directory.GetCurrentDirectory());
        _Logger.Information("ContentRoot: {Root}", App.Environment.ContentRootPath);
        var pagesDir = Path.Combine(App.Environment.ContentRootPath, "Pages");
        _Logger.Information("Pages Dir: {PagesDir}", pagesDir);
        if (Directory.Exists(pagesDir))
        {
            foreach (var file in Directory.GetFiles(pagesDir, "*.*", SearchOption.AllDirectories))
            {
                _Logger.Information("Pages file: {File}", file);
            }
        }
        else
        {
            _Logger.Warning("Pages directory does not exist: {PagesDir}", pagesDir);
        }

        // 3️⃣  Apply all queued middleware stages
        foreach (var stage in _middlewareQueue)
        {
            stage(App);
        }

        foreach (var feature in _featureQueue)
        {
            feature(this);
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

    public KestrunHost AddFeature(Action<KestrunHost> feature)
    {
        _featureQueue.Add(feature);
        return this;
    }

    public KestrunHost AddScheduling(int? MaxRunspaces = null)
    {
        if (MaxRunspaces is not null && MaxRunspaces <= 0)
            throw new ArgumentOutOfRangeException(nameof(MaxRunspaces), "MaxRunspaces must be greater than zero.");
        return AddFeature(host =>
        {
            if (_Logger.IsEnabled(LogEventLevel.Debug))
                _Logger.Debug("AddScheduling (deferred)");

            if (host.Scheduler is null)
            {
                if (MaxRunspaces is not null && MaxRunspaces > 0)
                {
                    _Logger.Information("Setting MaxSchedulerRunspaces to {MaxRunspaces}", MaxRunspaces);
                    host.Options.MaxSchedulerRunspaces = MaxRunspaces.Value;
                }
                _Logger.Verbose("Creating SchedulerService with MaxSchedulerRunspaces={MaxRunspaces}",
                    host.Options.MaxSchedulerRunspaces);
                var pool = host.CreateRunspacePool(host.Options.MaxSchedulerRunspaces);
                var logger = Log.Logger.ForContext<KestrunHost>();
                host.Scheduler = new SchedulerService(pool, logger);
            }
            else
            {
                _Logger.Warning("SchedulerService already configured; skipping.");
            }
        });
    }

    /// <summary>
    /// Adds Razor Pages to the application.
    /// </summary>
    /// <param name="cfg">The configuration options for Razor Pages.</param>
    /// <returns>The current KestrunHost instance.</returns>
    public KestrunHost AddRazorPages(RazorPagesOptions? cfg)
    {
        if (_Logger.IsEnabled(LogEventLevel.Debug))
            _Logger.Debug("Adding Razor Pages from source: {Source}", cfg);

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
        if (_Logger.IsEnabled(LogEventLevel.Debug))
            _Logger.Debug("Adding Razor Pages with configuration: {Config}", cfg);
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
        if (_Logger.IsEnabled(LogEventLevel.Debug))
            _Logger.Debug("Adding response compression with options: {@Options}", options);
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
        if (_Logger.IsEnabled(LogEventLevel.Debug))
            _Logger.Debug("Adding response compression with configuration: {Config}", cfg);
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

    public KestrunHost AddRateLimiter(RateLimiterOptions cfg)
    {
        if (_Logger.IsEnabled(LogEventLevel.Debug))
            _Logger.Debug("Adding rate limiter with configuration: {@Config}", cfg);
        if (cfg == null)
            return AddRateLimiter();   // fall back to your “blank” overload

        AddService(services =>
        {
            services.AddRateLimiter(opts => opts.CopyFrom(cfg));   // ← single line!
        });

        return Use(app => app.UseRateLimiter());
    }


    public KestrunHost AddRateLimiter(Action<RateLimiterOptions>? cfg = null)
    {
        if (_Logger.IsEnabled(LogEventLevel.Debug))
            _Logger.Debug("Adding rate limiter with configuration: {HasConfig}", cfg != null);

        // Register the rate limiter service
        AddService(services =>
        {
            services.AddRateLimiter(cfg ?? (_ => { })); // Always pass a delegate
        });

        // Apply the middleware
        return Use(app =>
        {
            if (_Logger.IsEnabled(LogEventLevel.Debug))
                _Logger.Debug("Registering rate limiter middleware");
            app.UseRateLimiter();
        });
    }


    /// <summary>
    /// Adds static files to the application.
    /// This overload allows you to specify configuration options.
    /// </summary>
    /// <param name="cfg">The static file options to configure.</param>
    /// <returns>The current KestrunHost instance.</returns>
    public KestrunHost AddStaticFiles(Action<StaticFileOptions>? cfg = null)
    {
        if (_Logger.IsEnabled(LogEventLevel.Debug))
            _Logger.Debug("Adding static files with configuration: {Config}", cfg);

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
        if (_Logger.IsEnabled(LogEventLevel.Debug))
            _Logger.Debug("Adding static files with options: {@Options}", options);

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
        if (_Logger.IsEnabled(LogEventLevel.Debug))
            _Logger.Debug("Adding Antiforgery with configuration: {@Config}", options);

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
        if (_Logger.IsEnabled(LogEventLevel.Debug))
            _Logger.Debug("Adding Antiforgery with configuration: {@Config}", setupAction);
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
        if (_Logger.IsEnabled(LogEventLevel.Debug))
            _Logger.Debug("Adding CORS policy: {PolicyName}", policyName);
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
        if (_Logger.IsEnabled(LogEventLevel.Debug))
            _Logger.Debug("Adding PowerShell runtime with route prefix: {RoutePrefix}", routePrefix);
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
        if (_Logger.IsEnabled(LogEventLevel.Debug))
            _Logger.Debug("Adding PowerShell Razor Pages with route prefix: {RoutePrefix}, config: {@Config}", routePrefix, cfg);

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
        if (_Logger.IsEnabled(LogEventLevel.Debug))
            _Logger.Debug("Adding PowerShell Razor Pages with route prefix: {RoutePrefix}, config: {@Config}", routePrefix, cfg);
        /*AddService(services =>
        {
            if (Logger.IsEnabled(LogEventLevel.Debug))
                Logger.Debug("Adding PowerShell Razor Pages to the service with route prefix: {RoutePrefix}", routePrefix);
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
                              Logger.Error(d.ToString());                 // or Console.WriteLine …
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
                       Logger.Error(d.ToString());
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
            if (_Logger.IsEnabled(LogEventLevel.Debug))
                _Logger.Debug("Adding PowerShell Razor Pages middleware with route prefix: {RoutePrefix}", routePrefix);


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

            }

            if (_Logger.IsEnabled(LogEventLevel.Debug))
                _Logger.Debug("PowerShell Razor Pages middleware added with route prefix: {RoutePrefix}", routePrefix);
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
        if (_Logger.IsEnabled(LogEventLevel.Debug))
            _Logger.Debug("Adding Default Files with configuration: {@Config}", cfg);

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


    public KestrunHost AddFavicon(string? iconPath = null)
    {
        return Use(app =>
        {
            app.UseFavicon(iconPath);
        });
    }


    public KestrunHost AddAuthentication(
        string defaultScheme,
        Action<AuthenticationBuilder> buildPolicy, Action<AuthorizationOptions>? configureAuthz = null)
    {
        ArgumentNullException.ThrowIfNull(buildPolicy);

        // ① Add authentication services via DI
        AddService(services =>
        {
            var builder = services.AddAuthentication(defaultScheme);
            buildPolicy(builder);  // ⬅️ Now you apply the user-supplied schemes here

            if (configureAuthz is null)
                services.AddAuthorization();                // default options
            else
                services.AddAuthorization(configureAuthz);  // caller customises
        });

        // ② Add middleware to enable auth pipeline
        return Use(app =>
        {
            app.UseAuthentication();
            app.UseAuthorization(); // optional but useful
        });
    }

    public KestrunHost AddAuthentication(
    Action<AuthenticationBuilder> buildSchemes,            // ← unchanged
    string defaultScheme = JwtBearerDefaults.AuthenticationScheme,
    Action<AuthorizationOptions>? configureAuthz = null)
    {
        AddService(services =>
        {
            var ab = services.AddAuthentication(defaultScheme);
            buildSchemes(ab);                                  // Basic + JWT here

            // make sure UseAuthorization() can find its services
            if (configureAuthz is null)
                services.AddAuthorization();
            else
                services.AddAuthorization(configureAuthz);
        });

        return Use(app =>
        {
            app.UseAuthentication();
            app.UseAuthorization();
        });
    }


    public KestrunHost AddAuthorization(Action<AuthorizationOptions>? cfg = null)
    {
        return AddService(s =>
        {
            if (cfg == null)
                s.AddAuthorization();
            else
                s.AddAuthorization(cfg);
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
        if (_Logger.IsEnabled(LogEventLevel.Debug))
            _Logger.Debug("Adding File Server with configuration: {@Config}", cfg);
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
        if (_Logger.IsEnabled(LogEventLevel.Debug))
            _Logger.Debug("Adding File Server with configuration: {@Config}", cfg);
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
        if (_Logger.IsEnabled(LogEventLevel.Debug))
            _Logger.Debug("Run() called");
        EnableConfiguration();

        App?.Run();
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_Logger.IsEnabled(LogEventLevel.Debug))
            _Logger.Debug("StartAsync() called");
        EnableConfiguration();
        if (App != null)
        {
            await App.StartAsync(cancellationToken);
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_Logger.IsEnabled(LogEventLevel.Debug))
            _Logger.Debug("StopAsync() called");
        if (App != null)
        {
            try
            {
                // Initiate graceful shutdown
                await App.StopAsync(cancellationToken);
            }
            catch (Exception ex) when (ex.GetType().FullName == "System.Net.Quic.QuicException")
            {
                // QUIC exceptions can occur during shutdown, especially if the server is not using QUIC.
                // We log this as a debug message to avoid cluttering the logs with expected exceptions.
                // This is a workaround for

                _Logger.Debug("Ignored QUIC exception during shutdown: {Message}", ex.Message);
            }
        }
    }

    public void Stop()
    {
        if (_Logger.IsEnabled(LogEventLevel.Debug))
            _Logger.Debug("Stop() called");
        // This initiates a graceful shutdown.
        App?.Lifetime.StopApplication();
    }

    #endregion



    #region Runspace Pool Management



    public KestrunRunspacePoolManager CreateRunspacePool(int? maxRunspaces = 0)
    {
        if (_Logger.IsEnabled(LogEventLevel.Debug))
            _Logger.Debug("CreateRunspacePool() called: {@MaxRunspaces}", maxRunspaces);

        // Create a default InitialSessionState with an unrestricted policy:
        var iss = InitialSessionState.CreateDefault();

        iss.ExecutionPolicy = Microsoft.PowerShell.ExecutionPolicy.Unrestricted;

        foreach (var p in _modulePaths)
        {
            iss.ImportPSModule([p]);
        }
        iss.Variables.Add(
            new SessionStateVariableEntry(
                "KestrunHost",
                this,
                "The KestrunHost instance"
            )
        );
        // Inject global variables into all runspaces
        foreach (var kvp in SharedStateStore.Snapshot())
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

        _Logger.Information($"Creating runspace pool with max runspaces: {maxRs}");
        var runspacePool = new KestrunRunspacePoolManager(Options?.MinRunspaces ?? 1, maxRunspaces: maxRs, initialSessionState: iss);
        // Return the created runspace pool
        return runspacePool;
    }


    #endregion


    #region Disposable

    public void Dispose()
    {
        if (_Logger.IsEnabled(LogEventLevel.Debug))
            _Logger.Debug("Dispose() called");
        _runspacePool?.Dispose();
        _runspacePool = null; // Clear the runspace pool reference
        _isConfigured = false; // Reset configuration state 
        App = null;
        Scheduler?.Dispose();
        //  Log.CloseAndFlush(); 
        (_Logger as IDisposable)?.Dispose();
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
        if (_Logger.IsEnabled(LogEventLevel.Debug))
            _Logger.Debug("ValidateCSharpScript() called: {@CodeLength}, imports={ImportsCount}, refs={RefsCount}, lang={Lang}",
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
        if (_Logger.IsEnabled(LogEventLevel.Debug))
            _Logger.Debug("GetCSharpScriptErrors() called: {@CodeLength}, imports={ImportsCount}, refs={RefsCount}, lang={Lang}",
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
