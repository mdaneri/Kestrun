

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
using static Kestrun.KestrunHost;

namespace Kestrun.Languages;
// ---------------------------------------------------------------------------
//  C# delegate builder  –  now takes optional imports / references
// ---------------------------------------------------------------------------

internal static class CSharpDelegateBuilder
{
    // 1. Inject each global as a top-level script variable
    // 2. Compile once
    // 3. Build the per-request delegate

    // ── configuration ─────────────────────────────────────────────── 
   // public record CsGlobals(KestrunRequest Request, KestrunResponse Response, HttpContext Context, IReadOnlyDictionary<string, object?> Globals);
    internal static RequestDelegate Build(
            string code, Serilog.ILogger log, string[]? extraImports,
            Assembly[]? extraRefs, LanguageVersion languageVersion = LanguageVersion.CSharp12)
    {
        if (log.IsEnabled(LogEventLevel.Debug))
            log.Debug("Building C# delegate, script length={Length}, imports={ImportsCount}, refs={RefsCount}, lang={Lang}",
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

        // 3. Build the per-request delegate
        return async context =>
        {
            try
            {
                var krRequest = await KestrunRequest.NewRequest(context);
                var krResponse = new KestrunResponse(krRequest);
                var Context = new KestrunContext(krRequest, krResponse, context);
                await script.RunAsync(new CsGlobals(allGlobals, Context)).ConfigureAwait(false);

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
}