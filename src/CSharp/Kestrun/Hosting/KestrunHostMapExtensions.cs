
using Kestrun.Hosting.Options;
using Kestrun.Languages;
using Kestrun.Scripting;
using Kestrun.Utilities;
using Microsoft.AspNetCore.Authorization;
using Serilog.Events;
using Kestrun.Models;
using System.Reflection;

namespace Kestrun.Hosting;

/// <summary>
/// Provides extension methods for mapping routes and handlers to the KestrunHost.
/// </summary>
public static class KestrunHostMapExtensions
{

    /// <summary>
    /// Represents a delegate that handles a Kestrun request with the provided context.
    /// </summary>
    /// <param name="Context">The context for the Kestrun request.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public delegate Task KestrunHandler(KestrunContext Context);

    /// <summary>
    /// Adds a native route to the KestrunHost for the specified pattern and HTTP verb.
    /// </summary>
    /// <param name="host">The KestrunHost instance.</param>
    /// <param name="pattern">The route pattern.</param>
    /// <param name="httpVerb">The HTTP verb for the route.</param>
    /// <param name="handler">The handler to execute for the route.</param>
    /// <param name="requireAuthorization">Optional array of authorization schemes required for the route.</param>
    /// <returns>An IEndpointConventionBuilder for further configuration.</returns>
    public static IEndpointConventionBuilder AddMapRoute(this KestrunHost host, string pattern, HttpVerb httpVerb, KestrunHandler handler, string[]? requireAuthorization = null)
    {
        if (host._Logger.IsEnabled(LogEventLevel.Debug))
            host._Logger.Debug("AddMapRoute called with pattern={Pattern}, httpVerb={HttpVerb}", pattern, httpVerb);
        return host.AddMapRoute(pattern: pattern, httpVerbs: [httpVerb], handler: handler, requireAuthorization: requireAuthorization);
    }

    /// <summary>
    /// Adds a native route to the KestrunHost for the specified pattern and HTTP verbs.
    /// </summary>
    /// <param name="host">The KestrunHost instance.</param>
    /// <param name="pattern">The route pattern.</param>
    /// <param name="httpVerbs">The HTTP verbs for the route.</param>
    /// <param name="handler">The handler to execute for the route.</param>
    /// <param name="requireAuthorization">Optional array of authorization schemes required for the route.</param>
    /// <returns>An IEndpointConventionBuilder for further configuration.</returns>
    public static IEndpointConventionBuilder AddMapRoute(this KestrunHost host, string pattern, IEnumerable<HttpVerb> httpVerbs, KestrunHandler handler, string[]? requireAuthorization = null)
    {
        if (host._Logger.IsEnabled(LogEventLevel.Debug))
            host._Logger.Debug("AddMapRoute called with pattern={Pattern}, httpVerbs={HttpVerbs}", pattern, string.Join(", ", httpVerbs));

        return host.AddMapRoute(new MapRouteOptions
        {
            Pattern = pattern,
            HttpVerbs = httpVerbs,
            Language = ScriptLanguage.Native,
            RequireAuthorization = requireAuthorization ?? [] // No authorization by default
        }, handler);

    }

    /// <summary>
    /// Adds a native route to the KestrunHost using the specified MapRouteOptions and handler.
    /// </summary>
    /// <param name="host">The KestrunHost instance.</param>
    /// <param name="options">The MapRouteOptions containing route configuration.</param>
    /// <param name="handler">The handler to execute for the route.</param>
    /// <returns>An IEndpointConventionBuilder for further configuration.</returns>
    public static IEndpointConventionBuilder AddMapRoute(this KestrunHost host, MapRouteOptions options, KestrunHandler handler)
    {
        if (host._Logger.IsEnabled(LogEventLevel.Debug))
            host._Logger.Debug("AddMapRoute called with pattern={Pattern}, method={Methods}", options.Pattern, string.Join(", ", options.HttpVerbs));
        // Ensure the WebApplication is initialized
        if (host.App is null)
            throw new InvalidOperationException("WebApplication is not initialized. Call EnableConfiguration first.");

        // Validate options
        if (string.IsNullOrWhiteSpace(options.Pattern))
            throw new ArgumentException("Pattern cannot be null or empty.", nameof(options.Pattern));

        string[] methods = [.. options.HttpVerbs.Select(v => v.ToMethodString())];
        var map = host.App.MapMethods(options.Pattern, methods, async context =>
           {
               var req = await KestrunRequest.NewRequest(context);
               var res = new KestrunResponse(req);
               KestrunContext kestrunContext = new(req, res, context);
               await handler(kestrunContext);
               await res.ApplyTo(context.Response);
           });

        host.AddMapOptions(map, options);

        host._Logger.Information("Added native route: {Pattern} with methods: {Methods}", options.Pattern, string.Join(", ", methods));
        // Add to the feature queue for later processing
        host.FeatureQueue.Add(host => host.AddMapRoute(options));
        return map;
    }


    /// <summary>
    /// Adds a route to the KestrunHost that executes a script block for the specified HTTP verb and pattern.
    /// </summary>
    /// <param name="host">The KestrunHost instance.</param>
    /// <param name="pattern">The route pattern.</param>
    /// <param name="httpVerbs">The HTTP verb for the route.</param>
    /// <param name="scriptBlock">The script block to execute.</param>
    /// <param name="language">The scripting language to use (default is PowerShell).</param>
    /// <param name="requireAuthorization">Optional array of authorization schemes required for the route.</param>
    /// <param name="arguments">Optional dictionary of arguments to pass to the script.</param>
    /// <returns>An IEndpointConventionBuilder for further configuration.</returns>
    public static IEndpointConventionBuilder AddMapRoute(this KestrunHost host, string pattern, HttpVerb httpVerbs, string scriptBlock, ScriptLanguage language = ScriptLanguage.PowerShell,
                                     string[]? requireAuthorization = null,
                                 Dictionary<string, object?>? arguments = null)
    {
        arguments ??= [];
        return host.AddMapRoute(new MapRouteOptions
        {
            Pattern = pattern,
            HttpVerbs = [httpVerbs],
            Code = scriptBlock,
            Language = language,
            RequireAuthorization = requireAuthorization ?? [], // No authorization by default
            Arguments = arguments ?? [] // No additional arguments by default
        });

    }

    /// <summary>
    /// Adds a route to the KestrunHost that executes a script block for the specified HTTP verbs and pattern.
    /// </summary>
    /// <param name="host">The KestrunHost instance.</param>
    /// <param name="pattern">The route pattern.</param>
    /// <param name="httpVerbs">The HTTP verbs for the route.</param>
    /// <param name="scriptBlock">The script block to execute.</param>
    /// <param name="language">The scripting language to use (default is PowerShell).</param>
    /// <param name="requireAuthorization">Optional array of authorization schemes required for the route.</param>
    /// <param name="arguments">Optional dictionary of arguments to pass to the script.</param>
    /// <returns>An IEndpointConventionBuilder for further configuration.</returns>
    public static IEndpointConventionBuilder AddMapRoute(this KestrunHost host, string pattern,
                                IEnumerable<HttpVerb> httpVerbs,
                                string scriptBlock,
                                ScriptLanguage language = ScriptLanguage.PowerShell,
                                string[]? requireAuthorization = null,
                                 Dictionary<string, object?>? arguments = null)
    {
        return host.AddMapRoute(new MapRouteOptions
        {
            Pattern = pattern,
            HttpVerbs = httpVerbs,
            Code = scriptBlock,
            Language = language,
            RequireAuthorization = requireAuthorization ?? [], // No authorization by default            
            Arguments = arguments ?? [] // No additional arguments by default
        });
    }
    /// <summary>
    /// Adds a route to the KestrunHost using the specified MapRouteOptions.
    /// </summary>
    /// <param name="host">The KestrunHost instance.</param>
    /// <param name="options">The MapRouteOptions containing route configuration.</param>
    /// <returns>An IEndpointConventionBuilder for further configuration.</returns>
    public static IEndpointConventionBuilder AddMapRoute(this KestrunHost host, MapRouteOptions options)
    {
        if (host._Logger.IsEnabled(LogEventLevel.Debug))
            host._Logger.Debug("AddMapRoute called with pattern={Pattern}, language={Language}, method={Methods}", options.Pattern, options.Language, options.HttpVerbs);

        // Ensure the WebApplication is initialized
        if (host.App is null)
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
            var logger = host._Logger.ForContext("Route", routeOptions.Pattern);
            // compile once – return an HttpContext->Task delegate
            var handler = options.Language switch
            {

                ScriptLanguage.PowerShell => PowerShellDelegateBuilder.Build(options.Code, logger, options.Arguments),
                ScriptLanguage.CSharp => CSharpDelegateBuilder.Build(options.Code, logger, options.Arguments, options.ExtraImports, options.ExtraRefs),
                ScriptLanguage.VBNet => VBNetDelegateBuilder.Build(options.Code, logger, options.Arguments, options.ExtraImports, options.ExtraRefs),
                ScriptLanguage.FSharp => FSharpDelegateBuilder.Build(options.Code, logger), // F# scripting not implemented
                ScriptLanguage.Python => PyDelegateBuilder.Build(options.Code, logger),
                ScriptLanguage.JavaScript => JScriptDelegateBuilder.Build(options.Code, logger),

                _ => throw new NotSupportedException(options.Language.ToString())
            };
            string[] methods = [.. options.HttpVerbs.Select(v => v.ToMethodString())];
            var map = host.App.MapMethods(options.Pattern, methods, handler).WithLanguage(options.Language);
            if (host._Logger.IsEnabled(LogEventLevel.Debug))
                host._Logger.Debug("Mapped route: {Pattern} with methods: {Methods}", options.Pattern, string.Join(", ", methods));

            host.AddMapOptions(map, options);

            host._Logger.Information("Added route: {Pattern} with methods: {Methods}", options.Pattern, string.Join(", ", methods));
            return map;
            // Add to the feature queue for later processing

        }
        catch (CompilationErrorException ex)
        {
            // Log the detailed compilation errors
            host._Logger.Error($"Failed to add route '{options.Pattern}' due to compilation errors:");
            host._Logger.Error(ex.GetDetailedErrorMessage());

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


    private static void AddMapOptions(this KestrunHost host, IEndpointConventionBuilder map, MapRouteOptions options)
    {
        if (options.ShortCircuit)
        {
            host._Logger.Verbose("Short-circuiting route: {Pattern} with status code: {StatusCode}", options.Pattern, options.ShortCircuitStatusCode);
            if (options.ShortCircuitStatusCode is null)
                throw new ArgumentException("ShortCircuitStatusCode must be set if ShortCircuit is true.", nameof(options.ShortCircuitStatusCode));
            map.ShortCircuit(options.ShortCircuitStatusCode);
        }

        if (options.AllowAnonymous) // Allow anonymous access to this route
        {
            host._Logger.Verbose("Allowing anonymous access for route: {Pattern}", options.Pattern);
            map.AllowAnonymous();
        }
        else
        {
            host._Logger.Debug("No anonymous access allowed for route: {Pattern}", options.Pattern);
        }

        if (options.DisableAntiforgery) // Disable CSRF protection for this route
        {
            map.DisableAntiforgery(); // Disable CSRF protection for this route
            host._Logger.Verbose("CSRF protection disabled for route: {Pattern}", options.Pattern);
        }

        if (!string.IsNullOrWhiteSpace(options.RateLimitPolicyName)) // Apply rate limiting policy if specified
        {
            host._Logger.Verbose("Applying rate limit policy: {RateLimitPolicyName} to route: {Pattern}", options.RateLimitPolicyName, options.Pattern);
            // Ensure RateLimiting is configured in the app
            map.RequireRateLimiting(options.RateLimitPolicyName);
        }
        if (options.RequireAuthorization is { Length: > 0 })
        {
            host._Logger.Verbose("Requiring authorization for route: {Pattern} with policies: {Policies}", options.Pattern, string.Join(", ", options.RequireAuthorization));
            map.RequireAuthorization(new AuthorizeAttribute
            {
                AuthenticationSchemes = string.Join(',', options.RequireAuthorization)
            });
        }
        else
        {
            host._Logger.Debug("No authorization required for route: {Pattern}", options.Pattern);
        }

        if (!string.IsNullOrWhiteSpace(options.CorsPolicyName)) // Apply CORS policy if specified
        {
            host._Logger.Verbose("Applying CORS policy: {CorsPolicyName} to route: {Pattern}", options.CorsPolicyName, options.Pattern);
            // Ensure CORS is configured in the app
            // apply the route-specific policy
            map.RequireCors(options.CorsPolicyName);
        }
        else
        {
            host._Logger.Debug("No CORS policy applied for route: {Pattern}", options.Pattern);
        }


        if (!string.IsNullOrEmpty(options.OpenAPI.OperationId))
        {
            host._Logger.Verbose("Adding OpenAPI metadata for route: {Pattern} with OperationId: {OperationId}", options.Pattern, options.OpenAPI.OperationId);
            // Add OpenAPI metadata if specified
            map.WithName(options.OpenAPI.OperationId);
        }

        if (!string.IsNullOrWhiteSpace(options.OpenAPI.Summary))
        {
            host._Logger.Verbose("Adding OpenAPI summary for route: {Pattern} with Summary: {Summary}", options.Pattern, options.OpenAPI.Summary);
            map.WithSummary(options.OpenAPI.Summary);
        }

        if (!string.IsNullOrWhiteSpace(options.OpenAPI.Description))
        {
            host._Logger.Verbose("Adding OpenAPI description for route: {Pattern} with Description: {Description}", options.Pattern, options.OpenAPI.Description);
            map.WithDescription(options.OpenAPI.Description);
        }
        if (options.OpenAPI.Tags.Length > 0)
        {
            host._Logger.Verbose("Adding OpenAPI tags for route: {Pattern} with Tags: {Tags}", options.Pattern, string.Join(", ", options.OpenAPI.Tags));
            map.WithTags(options.OpenAPI.Tags);
        }


        if (!string.IsNullOrWhiteSpace(options.OpenAPI.GroupName))
        {
            host._Logger.Verbose("Adding OpenAPI group name for route: {Pattern} with GroupName: {GroupName}", options.Pattern, options.OpenAPI.GroupName);
            map.WithGroupName(options.OpenAPI.GroupName);
        }
    }

    /// <summary>
    /// Adds an HTML template route to the KestrunHost for the specified pattern and HTML file path.
    /// </summary>
    /// <param name="host">The KestrunHost instance.</param>
    /// <param name="pattern">The route pattern.</param>
    /// <param name="htmlFilePath">The path to the HTML template file.</param>
    /// <param name="requireAuthorization">Optional array of authorization schemes required for the route.</param>
    /// <returns>An IEndpointConventionBuilder for further configuration.</returns>
    public static IEndpointConventionBuilder AddHtmlTemplateRoute(this KestrunHost host, string pattern, string htmlFilePath, string[]? requireAuthorization = null)
    {
        return host.AddHtmlTemplateRoute(new MapRouteOptions
        {
            Pattern = pattern,
            HttpVerbs = [HttpVerb.Get],
            RequireAuthorization = requireAuthorization ?? [] // No authorization by default
        }, htmlFilePath);
    }

    /// <summary>
    /// Adds an HTML template route to the KestrunHost using the specified MapRouteOptions and HTML file path.
    /// </summary>
    /// <param name="host">The KestrunHost instance.</param>
    /// <param name="options">The MapRouteOptions containing route configuration.</param>
    /// <param name="htmlFilePath">The path to the HTML template file.</param>
    /// <returns>An IEndpointConventionBuilder for further configuration.</returns>
    public static IEndpointConventionBuilder AddHtmlTemplateRoute(this KestrunHost host, MapRouteOptions options, string htmlFilePath)
    {

        if (host._Logger.IsEnabled(LogEventLevel.Debug))
            host._Logger.Debug("Adding HTML template route: {Pattern}", options.Pattern);

        if (options.HttpVerbs.Count() != 0 &&
            (options.HttpVerbs.Count() > 1 || options.HttpVerbs.First() != HttpVerb.Get))
        {
            host._Logger.Error("HTML template routes only support GET requests. Provided HTTP verbs: {HttpVerbs}", string.Join(", ", options.HttpVerbs));
            throw new ArgumentException("HTML template routes only support GET requests.", nameof(options.HttpVerbs));
        }
        if (string.IsNullOrWhiteSpace(htmlFilePath) || !File.Exists(htmlFilePath))
        {
            host._Logger.Error("HTML file path is null, empty, or does not exist: {HtmlFilePath}", htmlFilePath);
            throw new FileNotFoundException("HTML file not found.", htmlFilePath);
        }

        if (string.IsNullOrWhiteSpace(options.Pattern))
        {
            host._Logger.Error("Pattern cannot be null or empty.");
            throw new ArgumentException("Pattern cannot be null or empty.", nameof(options.Pattern));
        }

        var map = host.AddMapRoute(options.Pattern, HttpVerb.Get, async (ctx) =>
          {
              // ② Build your variables map
              var vars = new Dictionary<string, object?>();
              VariablesMap.GetVariablesMap(ctx, ref vars);

              await ctx.Response.WriteHtmlResponseFromFileAsync(htmlFilePath, vars, ctx.Response.StatusCode);
          });

        AddMapOptions(host, map, options);
        return map;
    }

    /// <summary>
    /// Adds a static override route to the KestrunHost for the specified pattern and handler.
    /// This allows you to override static file serving with dynamic content.
    /// Call this method before adding static file components to ensure it takes precedence.
    /// </summary>
    /// <param name="host">The KestrunHost instance.</param>
    /// <param name="pattern">The route pattern to match.</param>
    /// <param name="handler">The handler to execute for the route.</param>
    /// <param name="requireAuthorization">Optional array of authorization schemes required for the route.</param>
    /// <returns>The KestrunHost instance for method chaining.</returns>
    /// <remarks>
    /// This method allows you to override static file serving with dynamic content by providing a handler
    /// that will be executed for the specified route pattern.
    /// </remarks>
    public static KestrunHost AddStaticOverride(
        this KestrunHost host,
        string pattern,
        KestrunHandler handler,
        string[]? requireAuthorization = null)         // same delegate you already use
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pattern);

        return host.AddStaticOverride(new MapRouteOptions
        {
            Pattern = pattern,
            HttpVerbs = [HttpVerb.Get], // GET-only
            Language = ScriptLanguage.Native,
            RequireAuthorization = requireAuthorization ?? [] // No authorization by default
        }, handler);
    }

    /// <summary>
    /// Adds a static override route to the KestrunHost using the specified MapRouteOptions and handler.
    /// This allows you to override static file serving with dynamic content by providing a handler
    /// that will be executed for the specified route pattern.
    /// </summary>
    /// <param name="host">The KestrunHost instance.</param>
    /// <param name="options">The MapRouteOptions containing route configuration.</param>
    /// <param name="handler">The handler to execute for the route.</param>
    /// <returns>The KestrunHost instance for method chaining.</returns>
    public static KestrunHost AddStaticOverride(
        this KestrunHost host,
        MapRouteOptions options,
        KestrunHandler handler)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Pattern);

        // defer until WebApplication is being built
        return host.Use(app =>
        {
            var endpoints = (IEndpointRouteBuilder)app;        // ← key change

            // you said: static-file override should always be GET
            var map = endpoints.MapMethods(
                options.Pattern,
                [HttpMethods.Get],
                async ctx =>
                {
                    // wrap ASP.NET types in Kestrun abstractions
                    var req = await KestrunRequest.NewRequest(ctx);
                    var res = new KestrunResponse(req);
                    var kestrun = new KestrunContext(req, res, ctx);

                    await handler(kestrun);        // your logic
                    await res.ApplyTo(ctx.Response);
                });

            // apply ShortCircuit / CORS / auth etc.
            host.AddMapOptions(map, options);
        });
    }

    /// <summary>
    /// Adds a static override route to the KestrunHost for the specified pattern and script code.
    /// This allows you to override static file serving with dynamic content.
    /// /// Call this method before adding static file components to ensure it takes precedence.
    /// </summary>
    /// <param name="host">The KestrunHost instance.</param>
    /// <param name="pattern">The route pattern to match.</param>
    /// <param name="code">The script code to execute.</param>
    /// <param name="language">The scripting language to use.</param>
    /// <param name="requireAuthorization">Optional array of authorization schemes required for the route.</param>
    /// <param name="arguments">Optional dictionary of arguments to pass to the script.</param>
    /// <returns>The KestrunHost instance for method chaining.</returns>
    /// <remarks>
    /// This method allows you to override static file serving with dynamic content by providing a script
    /// that will be executed for the specified route pattern.
    /// </remarks> 
    public static KestrunHost AddStaticOverride(
    this KestrunHost host,
         string pattern,
         string code,
         ScriptLanguage language = ScriptLanguage.PowerShell,
         string[]? requireAuthorization = null,
         Dictionary<string, object?>? arguments = null
           )
    {
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(pattern);
            ArgumentException.ThrowIfNullOrWhiteSpace(code);

            var options = new MapRouteOptions
            {
                Pattern = pattern,
                HttpVerbs = [HttpVerb.Get], // GET-only
                Code = code,
                Language = language,
                RequireAuthorization = requireAuthorization ?? [], // No authorization by default
                Arguments = arguments ?? [], // No additional arguments by default

            };
            // queue before static files
            return host.Use(app =>
            {
                AddMapRoute(host, options);
            });
        }
    }


    /// <summary>
    /// Adds a static override route to the KestrunHost using the specified MapRouteOptions.
    /// This allows you to override static file serving with dynamic content.
    /// Call this method before adding static file components to ensure it takes precedence.
    /// </summary>
    /// <param name="host">The KestrunHost instance.</param>
    /// <param name="options">The MapRouteOptions containing route configuration.</param>
    /// <returns>The KestrunHost instance for method chaining.</returns>
    public static KestrunHost AddStaticOverride(this KestrunHost host, MapRouteOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Pattern);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Code);
        // queue before static files
        return host.Use(app =>
        {
            AddMapRoute(host, options);
        });
    }
}
