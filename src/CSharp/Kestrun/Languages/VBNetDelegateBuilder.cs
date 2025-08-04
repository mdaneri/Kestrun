using System.Collections.Immutable;
using System.Reflection;
using System.Text;
using Kestrun.Hosting;
using Kestrun.SharedState;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.CodeAnalysis.VisualBasic;
using Serilog;
using Serilog.Events;
using Kestrun.Models;
using Microsoft.CodeAnalysis;

namespace Kestrun.Languages;


internal static class VBNetDelegateBuilder
{
    /// <summary>
    /// Builds a VB.NET delegate for Kestrun routes.
    /// </summary>
    /// <remarks>
    /// This method uses the Roslyn compiler to compile the provided VB.NET code into a delegate.
    /// </remarks>
    /// <param name="code">The VB.NET code to compile.</param>
    /// <param name="log">The logger to use for logging compilation errors and warnings.</param>
    /// <param name="args">The arguments to pass to the script.</param>
    /// <param name="extraImports">Optional additional namespaces to import in the script.</param>
    /// <param name="extraRefs">Optional additional assemblies to reference in the script.</param>
    /// <param name="lang">The VB.NET language version to use for compilation.</param>
    /// <returns>A delegate that takes CsGlobals and returns a Task.</returns>
    /// <exception cref="CompilationErrorException">Thrown if the compilation fails with errors.</exception>
    /// <remarks>
    /// This method uses the Roslyn compiler to compile the provided VB.NET code into a delegate.
    /// </remarks>
    public static RequestDelegate Build(
        string code,
        Serilog.ILogger log,
        Dictionary<string, object?> args,
        string[]? extraImports,
        Assembly[]? extraRefs,
        Microsoft.CodeAnalysis.VisualBasic.LanguageVersion lang = Microsoft.CodeAnalysis.VisualBasic.LanguageVersion.VisualBasic16)
    {
        var run = Compile(code, log, extraImports, extraRefs, lang);

        return async ctx =>
        {
            var krRequest = await KestrunRequest.NewRequest(ctx);
            var krResponse = new KestrunResponse(krRequest);
            var glob = new Dictionary<string, object?>(SharedStateStore.Snapshot());
            var context = new KestrunContext(krRequest, krResponse, ctx);
            foreach (var kv in args) glob[kv.Key] = kv.Value;

            var globals = new CsGlobals(glob, context);
            await run(globals).ConfigureAwait(false);

            if (!string.IsNullOrEmpty(krResponse.RedirectUrl))
                ctx.Response.Redirect(krResponse.RedirectUrl);

            await krResponse.ApplyTo(ctx.Response);
        };
    }

    /// <summary>
    /// Compiles the provided VB.NET code into a delegate that can be executed with CsGlobals.
    /// </summary>
    /// <param name="code">The VB.NET code to compile.</param>
    /// <param name="log">The logger to use for logging compilation errors and warnings.</param>
    /// <param name="extraImports">Optional additional namespaces to import in the script.</param>
    /// <param name="extraRefs">Optional additional assemblies to reference in the script.</param>
    /// <param name="lang">The VB.NET language version to use for compilation.</param>
    /// <returns>A delegate that takes CsGlobals and returns a Task.</returns>
    /// <exception cref="CompilationErrorException">Thrown if the compilation fails with errors.</exception>
    /// <remarks>
    /// This method uses the Roslyn compiler to compile the provided VB.NET code into a delegate.
    /// </remarks>
    private static Func<CsGlobals, Task> Compile(
            string code,
            Serilog.ILogger log,
            IEnumerable<string>? extraImports,
            IEnumerable<Assembly>? extraRefs,
            Microsoft.CodeAnalysis.VisualBasic.LanguageVersion lang = LanguageVersion.VisualBasic16)
    {
        // 🔧 1.  Build a real VB file around the user snippet
        string source = BuildWrappedSource(code, extraImports);

        var tree = VisualBasicSyntaxTree.ParseText(
                       source,
                       new VisualBasicParseOptions(LanguageVersion.VisualBasic16));

        // 🔧 2.  References = everything already loaded  +  extras
        var refs = AppDomain.CurrentDomain.GetAssemblies()
                     .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
                     .Select(a => MetadataReference.CreateFromFile(a.Location))
                     .Concat(extraRefs?.Select(r => MetadataReference.CreateFromFile(r.Location))
                             ?? Enumerable.Empty<MetadataReference>())
                              .Append(             // ⬅ add VB runtime explicitly
                 MetadataReference.CreateFromFile(
                     typeof(Microsoft.VisualBasic.Constants).Assembly.Location));

        // 🔧 3.  Normal DLL compilation
        var compilation = VisualBasicCompilation.Create(
            assemblyName: $"RouteScript_{Guid.NewGuid():N}",
            syntaxTrees: new[] { tree },
            references: refs,
            options: new VisualBasicCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using var ms = new MemoryStream();
        var emitResult = compilation.Emit(ms);
        // Check for compilation errors
        if (emitResult.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error))
        {
            var errors = emitResult.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
            if (errors is not null && errors.Length > 0)
            {
                log.Error($"VBNet script compilation completed with {errors.Length} error(s):");
                foreach (var error in errors)
                {
                    var location = error.Location.IsInSource
                        ? $" at line {error.Location.GetLineSpan().StartLinePosition.Line + 1}"
                        : "";
                    log.Error($"  Error [{error.Id}]: {error.GetMessage()}{location}");
                }
                throw new CompilationErrorException("VBNet route code compilation failed", emitResult.Diagnostics);
            }
        }
        // Log warnings if any (optional - for debugging)
        var warnings = emitResult.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Warning).ToArray();
        if (warnings is not null && warnings.Length != 0)
        {
            log.Warning($"VBNet script compilation completed with {warnings.Length} warning(s):");
            foreach (var warning in warnings)
            {
                var location = warning.Location.IsInSource
                    ? $" at line {warning.Location.GetLineSpan().StartLinePosition.Line + 1}"
                    : "";
                log.Warning($"  Warning [{warning.Id}]: {warning.GetMessage()}{location}");
            }
        }
        ms.Position = 0;
        var asm = Assembly.Load(ms.ToArray());
        var runMethod = asm.GetType("RouteScript")!
                           .GetMethod("Run", BindingFlags.Public | BindingFlags.Static)!;

        // cast to a fast delegate (CsGlobals → Task)
        return (Func<CsGlobals, Task>)
       runMethod.CreateDelegate(typeof(Func<CsGlobals, Task>));

    }

    private static string BuildWrappedSource(string code, IEnumerable<string>? extraImports)
    {
        var sb = new StringBuilder();

        // common + caller-supplied Imports
        var builtIns = new[] {
        "System", "System.Threading.Tasks",
        "Kestrun", "Kestrun.Models",
          "Microsoft.VisualBasic",
          "Kestrun.Languages"
          };


        foreach (var ns in builtIns.Concat(extraImports ?? Enumerable.Empty<string>())
                                   .Distinct(StringComparer.Ordinal))
            sb.AppendLine($"Imports {ns}");

        sb.AppendLine("""
               Public Module RouteScript
                   Public Async Function Run(g As CsGlobals) As Task
                        Dim Request  = g.Context?.Request
                        Dim Response = g.Context?.Response
                        Dim Ctx      = g.Context
           """);
        /*
        sb.AppendLine("""
                Public Module RouteScript
                    Public Async Function Run() As Task         
            """);*/
        // indent the user snippet so VB is happy
        sb.AppendLine(String.Join(
            Environment.NewLine,
            code.Split('\n').Select(l => "        " + l.TrimEnd('\r'))));

        sb.AppendLine("""
            End Function
        End Module
    """);
        return sb.ToString();
    }

}