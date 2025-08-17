using System.Collections.Immutable;
using System.Reflection;
using System.Text;
using Kestrun.Hosting;
using Kestrun.SharedState;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Serilog;
using Serilog.Events;
using Kestrun.Models;
using System.Security.Claims;
using Kestrun.Logging;

namespace Kestrun.Languages;


internal static class CSharpDelegateBuilder
{
    /// <summary>
    /// Builds a C# delegate for handling HTTP requests.
    /// </summary>
    /// <param name="code">The C# code to execute.</param>
    /// <param name="log">The logger instance.</param>
    /// <param name="args">Arguments to inject as variables into the script.</param>
    /// <param name="extraImports">Additional namespaces to import.</param>
    /// <param name="extraRefs">Additional assemblies to reference.</param>
    /// <param name="languageVersion">The C# language version to use.</param>
    /// <returns>A delegate that handles HTTP requests.</returns>
    /// <exception cref="ArgumentNullException">Thrown if the code is null or whitespace.</exception>
    /// <exception cref="CompilationErrorException">Thrown if the C# code compilation fails.</exception>
    /// <remarks>   
    /// This method compiles the provided C# code into a script and returns a delegate that can be used to handle HTTP requests.
    /// It supports additional imports and references, and can inject global variables into the script.
    /// The delegate will execute the provided C# code within the context of an HTTP request, allowing access to the request and response objects.
    /// </remarks>
    internal static RequestDelegate Build(
            string code, Serilog.ILogger log, Dictionary<string, object?>? args, string[]? extraImports,
            Assembly[]? extraRefs, LanguageVersion languageVersion = LanguageVersion.CSharp12)
    {
        if (log.IsEnabled(LogEventLevel.Debug))
            log.Debug("Building C# delegate, script length={Length}, imports={ImportsCount}, refs={RefsCount}, lang={Lang}",
                code?.Length, extraImports?.Length ?? 0, extraRefs?.Length ?? 0, languageVersion);

        // Validate inputs
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentNullException(nameof(code), "C# code cannot be null or whitespace.");
        // 1. Compile the C# code into a script
        //    - Use CSharpScript.Create() to create a script with the provided code
        //    - Use ScriptOptions to specify imports, references, and language version
        //    - Inject the provided arguments into the globals
        var script = Compile(code, log, extraImports, extraRefs, null, languageVersion);

        // 2. Return a delegate that executes the script 
        //    - The delegate takes an HttpContext and returns a Task
        //    - It creates a KestrunContext and KestrunResponse from the HttpContext
        //    - It executes the script with the provided globals and locals
        //    - It applies the response to the HttpContext
        if (log.IsEnabled(LogEventLevel.Debug))
            log.Debug("C# delegate built successfully, script length={Length}, imports={ImportsCount}, refs={RefsCount}, lang={Lang}",
                code?.Length, extraImports?.Length ?? 0, extraRefs?.Length ?? 0, languageVersion);
        return async ctx =>
        {
            try
            {
                var krRequest = await KestrunRequest.NewRequest(ctx);
                var krResponse = new KestrunResponse(krRequest);
                var Context = new KestrunContext(krRequest, krResponse, ctx);
                if (log.IsEnabled(LogEventLevel.Debug))
                    log.DebugSanitized("Kestrun context created for {Path}", ctx.Request.Path);

                // Create a shared state dictionary that will be used to store global variables
                // This will be shared across all requests and can be used to store state
                // that needs to persist across multiple requests
                if (log.IsEnabled(LogEventLevel.Debug))
                    log.Debug("Creating shared state store for Kestrun context");
                var glob = new Dictionary<string, object?>(SharedStateStore.Snapshot());
                if (log.IsEnabled(LogEventLevel.Debug))
                    log.Debug("Shared state store created with {Count} items", glob.Count);

                // Inject the provided arguments into the globals
                // This allows the script to access these variables as if they were defined in the script itself
                // e.g. glob["arg1"] = args["arg1"];
                if (args != null && args.Count > 0)
                {
                    if (log.IsEnabled(LogEventLevel.Debug))
                        log.Debug("Setting C# variables from arguments: {Count}", args.Count);
                    foreach (var kv in args) glob[kv.Key] = kv.Value; // add args to globals
                }

                if (log.IsEnabled(LogEventLevel.Debug))
                    log.DebugSanitized("Executing C# script for {Path}", ctx.Request.Path);

                // Create a new CsGlobals instance with the current context and shared state
                // This will provide access to the globals and locals in the script
                var globals = new CsGlobals(glob, Context);

                // Execute the script with the current context and shared state
                if (log.IsEnabled(LogEventLevel.Debug))
                    log.DebugSanitized("Executing C# script for {Path}", ctx.Request.Path);
                await script.RunAsync(globals).ConfigureAwait(false);
                if (log.IsEnabled(LogEventLevel.Debug))
                    log.DebugSanitized("C# script executed successfully for {Path}", ctx.Request.Path);

                // Apply the response to the Kestrun context
                if (log.IsEnabled(LogEventLevel.Debug))
                    log.DebugSanitized("Applying response to Kestrun context for {Path}", ctx.Request.Path);
                if (!string.IsNullOrEmpty(krResponse.RedirectUrl))
                {
                    ctx.Response.Redirect(krResponse.RedirectUrl);
                    return;
                }

                await krResponse.ApplyTo(ctx.Response).ConfigureAwait(false);
                if (log.IsEnabled(LogEventLevel.Debug))
                    log.DebugSanitized("Response applied to Kestrun context for {Path}", ctx.Request.Path);
            }
            finally
            {
                await ctx.Response.CompleteAsync().ConfigureAwait(false);
            }
        };
    }


    /// <summary>
    /// Compiles the provided C# code into a script.
    /// This method supports additional imports and references, and can inject global variables into the script.
    /// It returns a compiled script that can be executed later.
    /// </summary>
    /// <param name="code">The C# code to compile.</param>
    /// <param name="log">The logger instance.</param>
    /// <param name="extraImports">Additional namespaces to import.</param>
    /// <param name="extraRefs">Additional assembly references.</param>
    /// <param name="locals">Local variables to inject into the script.</param>
    /// <param name="languageVersion">The C# language version to use.</param>
    /// <returns>A compiled script that can be executed later.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the code is null or whitespace.</exception>
    /// <exception cref="CompilationErrorException">Thrown when there are compilation errors.</exception>
    /// <remarks>
    /// This method compiles the provided C# code into a script using Roslyn.
    /// It supports additional imports and references, and can inject global variables into the script.
    /// The script can be executed later with the provided globals and locals.
    /// It is useful for scenarios where dynamic C# code execution is required, such as in web applications or scripting environments.
    /// </remarks>
    internal static Script<object> Compile(
            string? code, Serilog.ILogger log, string[]? extraImports,
            Assembly[]? extraRefs, IReadOnlyDictionary<string, object?>? locals, LanguageVersion languageVersion= LanguageVersion.CSharp12
            )
    {
        if (log.IsEnabled(LogEventLevel.Debug))
            log.Debug("Compiling C# script, length={Length}, imports={ImportsCount}, refs={RefsCount}, lang={Lang}",
                code?.Length, extraImports?.Length ?? 0, extraRefs?.Length ?? 0, languageVersion);

        // Validate inputs
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentNullException(nameof(code), "C# code cannot be null or whitespace.");

        // 1. Compose ScriptOptions
        var coreRefs = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),            // System.Private.CoreLib
            MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),        // System.Linq
            MetadataReference.CreateFromFile(typeof(HttpContext).Assembly.Location),       // Microsoft.AspNetCore.Http
            MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),          // System.Console
            MetadataReference.CreateFromFile(typeof(Serilog.Log).Assembly.Location),       // Serilog
            MetadataReference.CreateFromFile(typeof(ClaimsPrincipal).Assembly.Location)    // System.Security.Claims
        };
        // 2. Reference *your* Kestrun.dll once (contains Model, Hosting, etc.)
        var kestrunAssembly = typeof(Kestrun.Hosting.KestrunHost).Assembly;               // ← this *is* Kestrun.dll
        var kestrunRef = MetadataReference.CreateFromFile(kestrunAssembly.Location);

        // 3. Collect every exported namespace that starts with "Kestrun"
        var kestrunNamespaces = kestrunAssembly
            .GetExportedTypes()
            .Select(t => t.Namespace)
            .Where(ns => ns is { Length: > 0 } && ns.StartsWith("Kestrun", StringComparison.Ordinal))
            .Distinct()
            .ToArray();
        // Convert to MetadataReference for each namespace
        string[] platformImports = ["System", "System.Linq", "System.Threading.Tasks", "Microsoft.AspNetCore.Http",
            "System.Collections.Generic", "System.Security.Claims"];
        // Convert to MetadataReference for each namespace
        var allImports = platformImports.Concat(kestrunNamespaces);
        if (allImports is null)
            throw new ArgumentException("No valid imports found.", nameof(allImports));

        // 4. Create ScriptOptions with all imports and references
        //    - Use MetadataReference.CreateFromFile() for each assembly reference
        //    - Use .WithImports() to add all imports
        //    - Use .WithLanguageVersion() to set the C# language version
        var opts = ScriptOptions.Default
                   .WithImports((IEnumerable<string>)allImports)
                   .WithReferences(coreRefs.Concat([kestrunRef]));
                   
        extraImports ??= ["Kestrun"];
        if (!extraImports.Contains("Kestrun"))
        {
            var importsList = extraImports.ToList();
            importsList.Add("Kestrun");
            extraImports = [.. importsList];
        }
        if (extraImports is { Length: > 0 })
            opts = opts.WithImports(opts.Imports.Concat(extraImports));

        // Add extra assembly references
        if (extraRefs is { Length: > 0 })
        {
            foreach (var r in extraRefs)
            {
                if (string.IsNullOrEmpty(r.Location))
                    log.Warning("Skipping dynamic assembly with no location: {Assembly}", r.FullName);
                else if (!File.Exists(r.Location))
                    log.Warning("Skipping missing assembly file: {Location}", r.Location);
            }

            var safeRefs = extraRefs
                .Where(r => !string.IsNullOrEmpty(r.Location) && File.Exists(r.Location))
                .Select(r => MetadataReference.CreateFromFile(r.Location));

            opts = opts.WithReferences(opts.MetadataReferences.Concat(safeRefs));
        }

        // 1. Inject each global as a top-level script variable
        var allGlobals = SharedStateStore.Snapshot();
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
        if (locals != null && locals.Count > 0)
        {
            var sb = new StringBuilder();
            foreach (var kvp in locals)
            {
                sb.AppendLine(
                  $"var {kvp.Key} = ({kvp.Value?.GetType().FullName ?? "object"})Locals[\"{kvp.Key}\"];");
            }
            code = sb + code;
        }

        // 2. Compile once
        var script = CSharpScript.Create(code, opts, typeof(CsGlobals));
        ImmutableArray<Diagnostic>? diagnostics = null;
        try
        {
            diagnostics = script.Compile();
        }
        catch (CompilationErrorException ex)
        {
            log.Error(ex, "C# script compilation failed with errors.");
        }
        if (diagnostics == null)
        {
            log.Error("C# script compilation failed with no diagnostics.");
            throw new CompilationErrorException("C# script compilation failed with no diagnostics.", ImmutableArray<Diagnostic>.Empty);
        }
        // Check for compilation errors
        if (diagnostics?.Any(d => d.Severity == DiagnosticSeverity.Error) == true)
        {
            var errors = diagnostics?.Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
            if (errors is not null && errors.Length > 0)
            {
                log.Error($"C# script compilation completed with {errors.Length} error(s):");
                foreach (var error in errors)
                {
                    var location = error.Location.IsInSource
                        ? $" at line {error.Location.GetLineSpan().StartLinePosition.Line + 1}"
                        : "";
                    log.Error($"  Error [{error.Id}]: {error.GetMessage()}{location}");
                }
                throw new CompilationErrorException("C# route code compilation failed", diagnostics ?? []);
            }
        }
        // Log warnings if any (optional - for debugging)
        var warnings = diagnostics?.Where(d => d.Severity == DiagnosticSeverity.Warning).ToArray();
        if (warnings is not null && warnings.Length != 0)
        {
            log.Warning($"C# script compilation completed with {warnings.Length} warning(s):");
            foreach (var warning in warnings)
            {
                var location = warning.Location.IsInSource
                    ? $" at line {warning.Location.GetLineSpan().StartLinePosition.Line + 1}"
                    : "";
                log.Warning($"  Warning [{warning.Id}]: {warning.GetMessage()}{location}");
            }
        }
        // If there are no warnings, log a debug message
        if (warnings != null && warnings.Length == 0 && log.IsEnabled(LogEventLevel.Debug))
            log.Debug("C# script compiled successfully with no warnings.");

        return script;
    }
}