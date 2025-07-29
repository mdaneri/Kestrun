

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

namespace Kestrun.Languages;


internal static class CSharpDelegateBuilder
{

    /// <summary>
    /// Builds a C# delegate for handling HTTP requests.
    /// </summary>
    /// /// <param name="code">The C# code to execute.</param>
    /// <param name="log">The logger instance.</param>
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
            string code, Serilog.ILogger log, string[]? extraImports,
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
                await script.RunAsync(new CsGlobals(SharedStateStore.Snapshot(), Context)).ConfigureAwait(false);

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
        var diagnostics = script.Compile();

        // Check for compilation errors
        if (diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error))
            throw new CompilationErrorException("C# route code compilation failed", diagnostics);

        // Log warnings if any (optional - for debugging)
        var warnings = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Warning).ToArray();
        if (warnings.Length != 0)
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