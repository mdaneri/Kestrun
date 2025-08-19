using Microsoft.AspNetCore.Server.Kestrel.Core;
using System.Collections.Concurrent;
using System.Net;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using Kestrun.Utilities;
using Microsoft.CodeAnalysis;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using Serilog;
using Serilog.Events;
using Microsoft.AspNetCore.SignalR;
using Kestrun.Scheduling;
using Kestrun.SharedState;
using Kestrun.Middleware;
using Kestrun.Scripting;
using Kestrun.Hosting.Options;
using System.Runtime.InteropServices;
using Microsoft.PowerShell;

namespace Kestrun.Hosting;

/// <summary>
/// Provides hosting and configuration for the Kestrun application, including service registration, middleware setup, and runspace pool management.
/// </summary>
public class KestrunHost : IDisposable
{
    #region Fields

    // Shared state across routes
    private readonly ConcurrentDictionary<string, string> sharedState = new();
    private readonly WebApplicationBuilder builder;

    internal WebApplicationBuilder Builder => builder;

    private WebApplication? _app;

    internal WebApplication App
    {
        get
        {
            if (_app == null)
            {
                throw new InvalidOperationException("WebApplication is not built yet. Call Build() first.");
            }
            return _app;
        }
    }

    /// <summary>
    /// Gets the application name for the Kestrun host.
    /// </summary>
    public string ApplicationName => Options.ApplicationName ?? "KestrunApp";

    /// <summary>
    /// Gets the configuration options for the Kestrun host.
    /// </summary>
    public KestrunOptions Options { get; private set; } = new();
    private readonly List<string> _modulePaths = [];

    private bool _isConfigured;

    private KestrunRunspacePoolManager? _runspacePool;

    internal KestrunRunspacePoolManager RunspacePool
    {
        get
        {
            if (_runspacePool == null)
            {
                throw new InvalidOperationException("Runspace pool is not initialized. Call EnableConfiguration first.");
            }
            return _runspacePool;
        }
    }
    /// <summary>
    /// Gets the root directory path for the Kestrun application.
    /// </summary>
    public string? KestrunRoot { get; private set; }

    /// <summary>
    /// Gets the Serilog logger instance used by the Kestrun host.
    /// </summary>
    public Serilog.ILogger _Logger { get; private set; }

    /// <summary>
    /// Gets the scheduler service used for managing scheduled tasks in the Kestrun host.
    /// </summary>
    public SchedulerService Scheduler { get; internal set; } = null!; // Initialized in ConfigureServices

    // ── ✦ QUEUE #1 : SERVICE REGISTRATION ✦ ─────────────────────────────
    private readonly List<Action<IServiceCollection>> _serviceQueue = [];

    // ── ✦ QUEUE #2 : MIDDLEWARE STAGES ✦ ────────────────────────────────
    private readonly List<Action<IApplicationBuilder>> _middlewareQueue = [];

    private readonly List<Action<KestrunHost>> _featureQueue = [];

    internal List<Action<KestrunHost>> FeatureQueue => _featureQueue;

    internal readonly Dictionary<(string Pattern, string Method), MapRouteOptions> _registeredRoutes =
    new Dictionary<(string, string), MapRouteOptions>(
        new RouteKeyComparer());


    #endregion


    // Accepts optional module paths (from PowerShell)
    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="KestrunHost"/> class with the specified application name, root directory, and optional module paths.
    /// </summary>
    /// <param name="appName">The name of the application.</param>
    /// <param name="kestrunRoot">The root directory for the Kestrun application.</param>
    /// <param name="modulePathsObj">An array of module paths to be loaded.</param>
    public KestrunHost(string? appName, string? kestrunRoot = null, string[]? modulePathsObj = null) :
            this(appName, Log.Logger, kestrunRoot, modulePathsObj)
    { }

    /// <summary>
    /// Initializes a new instance of the <see cref="KestrunHost"/> class with the specified application name, logger, root directory, and optional module paths.
    /// </summary>
    /// <param name="appName">The name of the application.</param>
    /// <param name="logger">The Serilog logger instance to use.</param>
    /// <param name="kestrunRoot">The root directory for the Kestrun application.</param>
    /// <param name="modulePathsObj">An array of module paths to be loaded.</param>
    public KestrunHost(string? appName, Serilog.ILogger logger, string? kestrunRoot = null, string[]? modulePathsObj = null)
    {
        // ① Logger
        _Logger = logger ?? Log.Logger;
        LogConstructorArgs(appName, logger == null, kestrunRoot, modulePathsObj?.Length ?? 0);

        // ② Working directory/root
        SetWorkingDirectoryIfNeeded(kestrunRoot);

        // ③ Ensure Kestrun module path is available
        AddKestrunModulePathIfMissing(modulePathsObj);

        // ④ Builder + logging
        builder = WebApplication.CreateBuilder();
        builder.Host.UseSerilog();

        // ⑤ Options
        InitializeOptions(appName);

        // ⑥ Add user-provided module paths
        AddUserModulePaths(modulePathsObj);

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

    /// <summary>
    /// Logs constructor arguments at Debug level for diagnostics.
    /// </summary>
    private void LogConstructorArgs(string? appName, bool defaultLogger, string? kestrunRoot, int modulePathsLength)
    {
        if (_Logger.IsEnabled(LogEventLevel.Debug))
        {
            _Logger.Debug(
                "KestrunHost ctor: AppName={AppName}, DefaultLogger={DefaultLogger}, KestrunRoot={KestrunRoot}, ModulePathsLength={Len}",
                appName, defaultLogger, kestrunRoot, modulePathsLength);
        }
    }

    /// <summary>
    /// Sets the current working directory to the provided Kestrun root if needed and stores it.
    /// </summary>
    /// <param name="kestrunRoot">The Kestrun root directory path.</param>
    private void SetWorkingDirectoryIfNeeded(string? kestrunRoot)
    {
        if (string.IsNullOrWhiteSpace(kestrunRoot))
        {
            return;
        }

        if (!string.Equals(Directory.GetCurrentDirectory(), kestrunRoot, StringComparison.Ordinal))
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

    /// <summary>
    /// Ensures the core Kestrun module path is present; if missing, locates and adds it.
    /// </summary>
    /// <param name="modulePathsObj">The array of module paths to check.</param>
    private void AddKestrunModulePathIfMissing(string[]? modulePathsObj)
    {
        var needsLocate = modulePathsObj is null ||
                          (modulePathsObj?.Any(p => p.Contains("Kestrun.psm1", StringComparison.Ordinal)) == false);
        if (!needsLocate)
        {
            return;
        }

        var kestrunModulePath = PowerShellModuleLocator.LocateKestrunModule();
        if (string.IsNullOrWhiteSpace(kestrunModulePath))
        {
            _Logger.Fatal("Kestrun module not found. Ensure the Kestrun module is installed.");
            throw new FileNotFoundException("Kestrun module not found.");
        }

        _Logger.Information("Found Kestrun module at: {KestrunModulePath}", kestrunModulePath);
        _Logger.Verbose("Adding Kestrun module path: {KestrunModulePath}", kestrunModulePath);
        _modulePaths.Add(kestrunModulePath);
    }

    /// <summary>
    /// Initializes Kestrun options and sets the application name when provided.
    /// </summary>
    /// <param name="appName">The name of the application.</param>
    private void InitializeOptions(string? appName)
    {
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
    }

    /// <summary>
    /// Adds user-provided module paths if they exist, logging warnings for invalid entries.
    /// </summary>
    /// <param name="modulePathsObj">The array of module paths to check.</param>
    private void AddUserModulePaths(string[]? modulePathsObj)
    {
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
    }
    #endregion


    #region ListenerOptions 

    /// <summary>
    /// Configures a listener for the Kestrun host with the specified port, optional IP address, certificate, protocols, and connection logging.
    /// </summary>
    /// <param name="port">The port number to listen on.</param>
    /// <param name="ipAddress">The IP address to bind to. If null, binds to any address.</param>
    /// <param name="x509Certificate">The X509 certificate for HTTPS. If null, HTTPS is not used.</param>
    /// <param name="protocols">The HTTP protocols to use.</param>
    /// <param name="useConnectionLogging">Specifies whether to enable connection logging.</param>
    /// <returns>The current KestrunHost instance.</returns>
    public KestrunHost ConfigureListener(int port, IPAddress? ipAddress = null, X509Certificate2? x509Certificate = null, HttpProtocols protocols = HttpProtocols.Http1, bool useConnectionLogging = false)
    {
        if (_Logger.IsEnabled(LogEventLevel.Debug))
        {
            _Logger.Debug("ConfigureListener port={Port}, ipAddress={IPAddress}, protocols={Protocols}, useConnectionLogging={UseConnectionLogging}, certificate supplied={HasCert}", port, ipAddress, protocols, useConnectionLogging, x509Certificate != null);
        }

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

    /// <summary>
    /// Configures a listener for the Kestrun host with the specified port, optional IP address, and connection logging.
    /// </summary>
    /// <param name="port">The port number to listen on.</param>
    /// <param name="ipAddress">The IP address to bind to. If null, binds to any address.</param>
    /// <param name="useConnectionLogging">Specifies whether to enable connection logging.</param>
    public void ConfigureListener(int port, IPAddress? ipAddress = null, bool useConnectionLogging = false)
    {
        ConfigureListener(port: port, ipAddress: ipAddress, x509Certificate: null, protocols: HttpProtocols.Http1, useConnectionLogging: useConnectionLogging);
    }

    /// <summary>
    /// Configures a listener for the Kestrun host with the specified port and connection logging option.
    /// </summary>
    /// <param name="port">The port number to listen on.</param>
    /// <param name="useConnectionLogging">Specifies whether to enable connection logging.</param>
    public void ConfigureListener(int port, bool useConnectionLogging = false)
    {
        ConfigureListener(port: port, ipAddress: null, x509Certificate: null, protocols: HttpProtocols.Http1, useConnectionLogging: useConnectionLogging);
    }

    #endregion




    #region C#


    #endregion



    #region Route



    #endregion
    #region Configuration


    /// <summary>
    /// Applies the configured options to the Kestrel server and initializes the runspace pool.
    /// </summary>
    public void EnableConfiguration(Dictionary<string, object>? userVariables = null)
    {
        if (_Logger.IsEnabled(LogEventLevel.Debug))
        {
            _Logger.Debug("EnableConfiguration(options) called");
        }

        if (_isConfigured)
        {
            if (_Logger.IsEnabled(LogEventLevel.Debug))
            {
                _Logger.Debug("Configuration already applied, skipping");
            }

            return; // Already configured
        }
        try
        {
            // This method is called to apply the configured options to the Kestrel server.
            // The actual application of options is done in the Run method.
            _runspacePool = CreateRunspacePool(Options.MaxRunspaces, userVariables);
            if (_runspacePool == null)
            {
                throw new InvalidOperationException("Failed to create runspace pool.");
            }

            if (_Logger.IsEnabled(LogEventLevel.Verbose))
            {
                _Logger.Verbose("Runspace pool created with max runspaces: {MaxRunspaces}", Options.MaxRunspaces);
            }
            // Configure Kestrel
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
                            {
                                listenOptions.UseHttps(opt.X509Certificate);
                            }

                            if (opt.UseConnectionLogging)
                            {
                                listenOptions.UseConnectionLogging();
                            }
                        });
                    });
                }
            });


            _app = Build();
            var dataSource = _app.Services.GetRequiredService<EndpointDataSource>();

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
        if (builder == null)
        {
            throw new InvalidOperationException("Call CreateBuilder() first.");
        }

        // 1️⃣  Apply all queued services
        foreach (var configure in _serviceQueue)
        {
            configure(builder.Services);
        }

        // 2️⃣  Build the WebApplication
        _app = builder.Build();

        _Logger.Information("CWD: {CWD}", Directory.GetCurrentDirectory());
        _Logger.Information("ContentRoot: {Root}", _app.Environment.ContentRootPath);
        var pagesDir = Path.Combine(_app.Environment.ContentRootPath, "Pages");
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
            stage(_app);
        }

        foreach (var feature in _featureQueue)
        {
            feature(this);
        }
        // 5️⃣  Terminal endpoint execution 
        return _app;
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
    /// Adds a feature configuration action to the feature queue.
    /// This action will be executed when the features are applied.
    /// </summary>
    /// <param name="feature">The feature configuration action.</param>
    /// <returns>The current KestrunHost instance.</returns>
    public KestrunHost AddFeature(Action<KestrunHost> feature)
    {
        _featureQueue.Add(feature);
        return this;
    }

    /// <summary>
    /// Adds a scheduling feature to the Kestrun host, optionally specifying the maximum number of runspaces for the scheduler.
    /// </summary>
    /// <param name="MaxRunspaces">The maximum number of runspaces for the scheduler. If null, uses the default value.</param>
    /// <returns>The current KestrunHost instance.</returns>
    public KestrunHost AddScheduling(int? MaxRunspaces = null)
    {
        if (MaxRunspaces is not null && MaxRunspaces <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxRunspaces), "MaxRunspaces must be greater than zero.");
        }

        return AddFeature(host =>
        {
            if (_Logger.IsEnabled(LogEventLevel.Debug))
            {
                _Logger.Debug("AddScheduling (deferred)");
            }

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
    /// Adds MVC / API controllers to the application.
    /// </summary>
    /// <param name="cfg">The configuration options for MVC / API controllers.</param>
    /// <returns>The current KestrunHost instance.</returns>
    public KestrunHost AddControllers(Action<Microsoft.AspNetCore.Mvc.MvcOptions>? cfg = null)
    {
        return AddService(services =>
        {
            var builder = services.AddControllers();
            if (cfg != null)
            {
                builder.ConfigureApplicationPartManager(pm => { }); // customise if you wish
            }
        });
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
        {
            _Logger.Debug("Adding PowerShell runtime with route prefix: {RoutePrefix}", routePrefix);
        }

        return Use(app =>
        {
            ArgumentNullException.ThrowIfNull(_runspacePool);
            // ── mount PowerShell at the root ──
            app.UseLanguageRuntime(
                ScriptLanguage.PowerShell,
                b => b.UsePowerShellRunspace(_runspacePool));
        });
    }







    // ② SignalR
    /// <summary>
    /// Adds a SignalR hub to the application at the specified path.
    /// </summary>
    /// <typeparam name="T">The type of the SignalR hub.</typeparam>
    /// <param name="path">The path at which to map the SignalR hub.</param>
    /// <returns>The current KestrunHost instance.</returns>
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

    /// <summary>
    /// Runs the Kestrun web application, applying configuration and starting the server.
    /// </summary>
    public void Run()
    {
        if (_Logger.IsEnabled(LogEventLevel.Debug))
        {
            _Logger.Debug("Run() called");
        }

        EnableConfiguration();

        _app?.Run();
    }

    /// <summary>
    /// Starts the Kestrun web application asynchronously.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the asynchronous start operation.</returns>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_Logger.IsEnabled(LogEventLevel.Debug))
        {
            _Logger.Debug("StartAsync() called");
        }

        EnableConfiguration();
        if (_app != null)
        {
            await _app.StartAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Stops the Kestrun web application asynchronously.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the asynchronous stop operation.</returns>
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_Logger.IsEnabled(LogEventLevel.Debug))
        {
            _Logger.Debug("StopAsync() called");
        }

        if (_app != null)
        {
            try
            {
                // Initiate graceful shutdown
                await _app.StopAsync(cancellationToken);
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

    /// <summary>
    /// Initiates a graceful shutdown of the Kestrun web application.
    /// </summary>
    public void Stop()
    {
        if (_Logger.IsEnabled(LogEventLevel.Debug))
        {
            _Logger.Debug("Stop() called");
        }
        // This initiates a graceful shutdown.
        _app?.Lifetime.StopApplication();
    }

    /// <summary>
    /// Determines whether the Kestrun web application is currently running.
    /// </summary>
    /// <returns>True if the application is running; otherwise, false.</returns>
    public bool IsRunning
    {
        get
        {
            var appField = typeof(KestrunHost)
                .GetField("_app", BindingFlags.NonPublic | BindingFlags.Instance);

            return appField?.GetValue(this) is WebApplication app && !app.Lifetime.ApplicationStopping.IsCancellationRequested;
        }
    }


    #endregion



    #region Runspace Pool Management



    /// <summary>
    /// Creates and returns a new <see cref="KestrunRunspacePoolManager"/> instance with the specified maximum number of runspaces.
    /// </summary>
    /// <param name="maxRunspaces">The maximum number of runspaces to create. If not specified or zero, defaults to twice the processor count.</param>
    /// <param name="userVariables">A dictionary of user-defined variables to inject into the runspace pool.</param>
    /// <returns>A configured <see cref="KestrunRunspacePoolManager"/> instance.</returns>
    public KestrunRunspacePoolManager CreateRunspacePool(int? maxRunspaces = 0, Dictionary<string, object>? userVariables = null)
    {
        if (_Logger.IsEnabled(LogEventLevel.Debug))
        {
            _Logger.Debug("CreateRunspacePool() called: {@MaxRunspaces}", maxRunspaces);
        }

        // Create a default InitialSessionState with an unrestricted policy:
        var iss = InitialSessionState.CreateDefault();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            iss.ExecutionPolicy = ExecutionPolicy.Unrestricted;
        }

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

        foreach (var kvp in userVariables ?? [])
        {
            if (kvp.Value is PSVariable psVar)
            {
                iss.Variables.Add(
                    new SessionStateVariableEntry(
                        kvp.Key,
                        psVar.Value,
                        psVar.Description ?? "User-defined variable"
                    )
                );
            }
            else
            {
                iss.Variables.Add(
                    new SessionStateVariableEntry(
                        kvp.Key,
                        kvp.Value,
                        "User-defined variable"
                    )
                );
            }
        }
        int maxRs = (maxRunspaces.HasValue && maxRunspaces.Value > 0) ? maxRunspaces.Value : Environment.ProcessorCount * 2;

        _Logger.Information($"Creating runspace pool with max runspaces: {maxRs}");
        var runspacePool = new KestrunRunspacePoolManager(Options?.MinRunspaces ?? 1, maxRunspaces: maxRs, initialSessionState: iss);
        // Return the created runspace pool
        return runspacePool;
    }


    #endregion


    #region Disposable

    /// <summary>
    /// Releases all resources used by the <see cref="KestrunHost"/> instance.
    /// </summary>
    public void Dispose()
    {
        if (_Logger.IsEnabled(LogEventLevel.Debug))
        {
            _Logger.Debug("Dispose() called");
        }

        _runspacePool?.Dispose();
        _runspacePool = null; // Clear the runspace pool reference
        _isConfigured = false; // Reset configuration state 
        _app = null;
        Scheduler?.Dispose();
        (_Logger as IDisposable)?.Dispose();
    }
    #endregion

    #region Script Validation


    #endregion
}
