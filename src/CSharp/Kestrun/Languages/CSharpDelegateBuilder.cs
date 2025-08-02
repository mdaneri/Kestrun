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

namespace Kestrun.Languages;


internal static class CSharpDelegateBuilder
{

    /// <summary>
    /// Builds a C# delegate for handling HTTP requests.
    /// </summary>
    /// <param name="code">The C# code to execute.</param>
    /// <param name="log">The logger instance.</param>
    /// <param name="arguments">Arguments to inject as variables into the script.</param>
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
            string code, Serilog.ILogger log, Dictionary<string, object> arguments, string[]? extraImports,
            Assembly[]? extraRefs, LanguageVersion languageVersion = LanguageVersion.CSharp12)
    {
        if (log.IsEnabled(LogEventLevel.Debug))
            log.Debug("Building C# delegate, script length={Length}, imports={ImportsCount}, refs={RefsCount}, lang={Lang}",
                code?.Length, extraImports?.Length ?? 0, extraRefs?.Length ?? 0, languageVersion);

        // 1. Inject each global as a top-level script variable
        var script = Compile(code, log, extraImports, extraRefs, null, languageVersion);

        // 3. Build the per-request delegate
        return async context =>
        {
            try
            {
                var krRequest = await KestrunRequest.NewRequest(context);
                var krResponse = new KestrunResponse(krRequest);
                var Context = new KestrunContext(krRequest, krResponse, context);

                var state = new Dictionary<string, object?>(SharedStateStore.Snapshot());
                if (arguments != null && arguments.Count > 0)
                {
                    if (log.IsEnabled(LogEventLevel.Debug))
                        log.Debug("Setting C# variables from arguments: {Count}", arguments.Count);
                    foreach (var arg in arguments)
                    {
                        // Set the arguments as C# variables in the script
                        state[arg.Key] = arg.Value;
                    }
                }

                if (log.IsEnabled(LogEventLevel.Debug))
                    log.Debug("Executing C# script for {Path}", context.Request.Path);
                // Create a new script instance with the current context and shared state
                // Execute the script with the current context and shared state
                await script.RunAsync(new CsGlobals(state, Context)).ConfigureAwait(false);

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
    internal static Script<object> Compile(string? code, Serilog.ILogger log, string[]? extraImports,
            Assembly[]? extraRefs, IReadOnlyDictionary<string, object?>? locals, LanguageVersion languageVersion = LanguageVersion.CSharp12)
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
            MetadataReference.CreateFromFile(typeof(KestrunResponse).Assembly.Location),    // Kestrun.Response
            MetadataReference.CreateFromFile(typeof(KestrunRequest).Assembly.Location),     // Kestrun.Request
            MetadataReference.CreateFromFile(typeof(KestrunContext).Assembly.Location),     // Kestrun.Hosting
            MetadataReference.CreateFromFile(typeof(SharedStateStore).Assembly.Location),   // Kestrun.SharedState
            MetadataReference.CreateFromFile(typeof(CsGlobals).Assembly.Location)          // Kestrun.Languages            
        };

        var opts = ScriptOptions.Default
                   .WithImports("System", "System.Linq", "System.Threading.Tasks",
                                "Microsoft.AspNetCore.Http")
                   .WithReferences(coreRefs)                 // ✅ now uses MetadataReference[]
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
        return script;
    }

}