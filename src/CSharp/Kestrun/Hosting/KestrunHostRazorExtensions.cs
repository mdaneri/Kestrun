using System.Reflection;
using Kestrun.Razor;
using Microsoft.AspNetCore.Mvc.Razor.RuntimeCompilation;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.FileProviders;
using Serilog;
using Serilog.Events;

namespace Kestrun.Hosting;

/// <summary>
/// Provides extension methods for adding PowerShell and Razor Pages to a KestrunHost.
/// </summary>
public static class KestrunHostRazorExtensions
{
    /// <summary>
    /// Adds PowerShell Razor Pages to the application.
    /// This middleware allows you to serve Razor Pages using PowerShell scripts.
    /// </summary>
    /// <param name="host">The KestrunHost instance to add Razor Pages to.</param>
    /// <param name="routePrefix">The route prefix to use for the PowerShell Razor Pages.</param>
    /// <param name="cfg">Configuration options for the Razor Pages.</param>
    /// <returns>The current KestrunHost instance.</returns>
    public static KestrunHost AddPowerShellRazorPages(this KestrunHost host, PathString? routePrefix, RazorPagesOptions? cfg)
    {
        if (host._Logger.IsEnabled(LogEventLevel.Debug))
            host._Logger.Debug("Adding PowerShell Razor Pages with route prefix: {RoutePrefix}, config: {@Config}", routePrefix, cfg);

        return AddPowerShellRazorPages(host, routePrefix, dest =>
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
    /// <param name="host">The KestrunHost instance to add Razor Pages to.</param>
    /// <param name="routePrefix">The route prefix to use for the PowerShell Razor Pages.</param>
    /// <returns>The current KestrunHost instance.</returns>
    public static KestrunHost AddPowerShellRazorPages(this KestrunHost host, PathString? routePrefix) =>
        AddPowerShellRazorPages(host, routePrefix, (Action<RazorPagesOptions>?)null);

    /// <summary>
    /// Adds PowerShell Razor Pages to the application with default configuration and no route prefix.
    /// </summary>
    /// <param name="host">The KestrunHost instance to add Razor Pages to.</param>
    /// <returns>The current KestrunHost instance.</returns>
    public static KestrunHost AddPowerShellRazorPages(this KestrunHost host) =>
        AddPowerShellRazorPages(host, null, (Action<RazorPagesOptions>?)null);

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
    /// <param name="host">The KestrunHost instance to add Razor Pages to.</param>
    /// <param name="routePrefix">The route prefix to use for the PowerShell Razor Pages.</param>
    /// <param name="cfg">Configuration options for the Razor Pages.</param>
    /// <returns>The current KestrunHost instance.</returns>
    public static KestrunHost AddPowerShellRazorPages(this KestrunHost host, PathString? routePrefix, Action<RazorPagesOptions>? cfg = null)
    {
        if (host._Logger.IsEnabled(LogEventLevel.Debug))
            host._Logger.Debug("Adding PowerShell Razor Pages with route prefix: {RoutePrefix}, config: {@Config}", routePrefix, cfg);

        host.AddService(services =>
        {
            var env = host.Builder.Environment;
            if (host._Logger.IsEnabled(LogEventLevel.Debug))
                host._Logger.Debug("Adding PowerShell Razor Pages to the service with route prefix: {RoutePrefix}", routePrefix);

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
                var pagesRoot = Path.Combine(host.Builder.Environment.ContentRootPath, "Pages");
                if (Directory.Exists(pagesRoot))
                    opts.FileProviders.Add(new PhysicalFileProvider(pagesRoot));
            });
        });

        return host.Use(app =>
        {
            ArgumentNullException.ThrowIfNull(host.RunspacePool);
            if (host._Logger.IsEnabled(LogEventLevel.Debug))
                host._Logger.Debug("Adding PowerShell Razor Pages middleware with route prefix: {RoutePrefix}", routePrefix);

            if (routePrefix.HasValue)
            {
                // ── /ps  (or whatever prefix) ──────────────────────────────
                app.Map(routePrefix.Value, branch =>
                {
                    branch.UsePowerShellRazorPages(host.RunspacePool);   // bridge
                    branch.UseRouting();                             // add routing
                    branch.UseEndpoints(e => e.MapRazorPages());     // map pages
                });
            }
            else
            {
                // ── mounted at root ────────────────────────────────────────
                app.UsePowerShellRazorPages(host.RunspacePool);          // bridge
                app.UseRouting();                                    // add routing
                app.UseEndpoints(e => e.MapRazorPages());            // map pages
            }

            if (host._Logger.IsEnabled(LogEventLevel.Debug))
                host._Logger.Debug("PowerShell Razor Pages middleware added with route prefix: {RoutePrefix}", routePrefix);
        });
    }


    /// <summary>
    /// Adds Razor Pages to the application.
    /// </summary>
    /// <param name="host">The KestrunHost instance to add Razor Pages to.</param>
    /// <param name="cfg">The configuration options for Razor Pages.</param>
    /// <returns>The current KestrunHost instance.</returns>
    public static KestrunHost AddRazorPages(this KestrunHost host, RazorPagesOptions? cfg)
    {
        if (host._Logger.IsEnabled(LogEventLevel.Debug))
            host._Logger.Debug("Adding Razor Pages from source: {Source}", cfg);

        if (cfg == null)
            return host.AddRazorPages(); // no config, use defaults

        return host.AddRazorPages(dest =>
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
    /// <param name="host">The KestrunHost instance to add Razor Pages to.</param>
    /// <param name="cfg">The configuration options for Razor Pages.</param>
    /// <returns>The current KestrunHost instance.</returns>
    public static KestrunHost AddRazorPages(this KestrunHost host, Action<RazorPagesOptions>? cfg = null)
    {
        if (host._Logger.IsEnabled(LogEventLevel.Debug))
            host._Logger.Debug("Adding Razor Pages with configuration: {Config}", cfg);
        return host.AddService(services =>
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
}