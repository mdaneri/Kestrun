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
using Kestrun.Utilities;
using System.Security.Claims;
using Kestrun.Logging;
using Microsoft.AspNetCore.Http;

namespace Kestrun.Languages;


internal static class VBNetDelegateBuilder
{
    /// <summary>
    /// The marker that indicates where user code starts in the VB.NET script.
    /// This is used to ensure that the user code is correctly placed within the generated module.
    /// </summary>
    private const string StartMarker = "' ---- User code starts here ----";

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
    /// <param name="languageVersion">The VB.NET language version to use for compilation.</param>
    /// <returns>A delegate that takes CsGlobals and returns a Task.</returns>
    /// <exception cref="CompilationErrorException">Thrown if the compilation fails with errors.</exception>
    /// <remarks>
    /// This method uses the Roslyn compiler to compile the provided VB.NET code into a delegate.
    /// </remarks>
    internal static RequestDelegate Build(
        string code, Serilog.ILogger log, Dictionary<string, object?>? args, string[]? extraImports,
        Assembly[]? extraRefs, LanguageVersion languageVersion = LanguageVersion.VisualBasic16_9)
    {
        if (log.IsEnabled(LogEventLevel.Debug))
            log.Debug("Building VB.NET delegate, script length={Length}, imports={ImportsCount}, refs={RefsCount}, lang={Lang}",
               code.Length, extraImports?.Length ?? 0, extraRefs?.Length ?? 0, languageVersion);

        // Validate inputs
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentNullException(nameof(code), "VB.NET code cannot be null or whitespace.");
        // 1. Compile the VB.NET code into a script 
        //    - Use VisualBasicScript.Create() to create a script with the provided code
        //    - Use ScriptOptions to specify imports, references, and language version
        //    - Inject the provided arguments into the globals
        var script = Compile<bool>(code, log, extraImports, extraRefs, null, languageVersion);

        // 2. Build the per-request delegate
        //    - This delegate will be executed for each request
        //    - It will create a KestrunContext and CsGlobals, then execute the script with these globals
        //    - The script can access the request context and shared state store
        if (log.IsEnabled(LogEventLevel.Debug))
            log.Debug("C# delegate built successfully, script length={Length}, imports={ImportsCount}, refs={RefsCount}, lang={Lang}",
                code?.Length, extraImports?.Length ?? 0, extraRefs?.Length ?? 0, languageVersion);
        return async ctx =>
        {
            try
            {
               if (log.IsEnabled(LogEventLevel.Debug))
                    log.Debug("Preparing execution for C# script at {Path}", ctx.Request.Path);
                var (Globals, Response, Context) = await DelegateBuilder.PrepareExecutionAsync(ctx, log, args).ConfigureAwait(false);

           

                // Execute the script with the current context and shared state
                if (log.IsEnabled(LogEventLevel.Debug))
                    log.DebugSanitized("Executing VB.NET script for {Path}", ctx.Request.Path);
                await script(Globals).ConfigureAwait(false);
                if (log.IsEnabled(LogEventLevel.Debug))
                    log.DebugSanitized("VB.NET script executed successfully for {Path}", ctx.Request.Path);

                // Apply the response to the Kestrun context
                await DelegateBuilder.ApplyResponseAsync(ctx, Response, log).ConfigureAwait(false);
            }
            finally
            {
                await ctx.Response.CompleteAsync().ConfigureAwait(false);
            }
        };
    }




    // Decide the VB return type string that matches TResult
    private static string GetVbReturnType(Type t)
    {
        if (t == typeof(bool)) return "Boolean";

        if (t == typeof(IEnumerable<Claim>))
            return "System.Collections.Generic.IEnumerable(Of System.Security.Claims.Claim)";

        // Fallback so it still compiles even for object / string / etc.
        return "Object";
    }

    /// <summary>
    /// Compiles the provided VB.NET code into a delegate that can be executed with CsGlobals.
    /// </summary>
    /// <param name="code">The VB.NET code to compile.</param>
    /// <param name="log">The logger to use for logging compilation errors and warnings.</param>
    /// <param name="extraImports">Optional additional namespaces to import in the script.</param>
    /// <param name="extraRefs">Optional additional assemblies to reference in the script.</param>
    /// <param name="locals">Optional local variables to provide to the script.</param>
    /// <param name="languageVersion">The VB.NET language version to use for compilation.</param>
    /// <returns>A delegate that takes CsGlobals and returns a Task.</returns>
    /// <exception cref="CompilationErrorException">Thrown if the compilation fails with errors.</exception>
    /// <remarks>
    /// This method uses the Roslyn compiler to compile the provided VB.NET code into a delegate.
    /// </remarks>
    internal static Func<CsGlobals, Task<TResult>> Compile<TResult>(
            string? code, Serilog.ILogger log, string[]? extraImports,
            Assembly[]? extraRefs, IReadOnlyDictionary<string, object?>? locals, LanguageVersion languageVersion
        )
    {
        if (log.IsEnabled(LogEventLevel.Debug))
            log.Debug("Building VB.NET delegate, script length={Length}, imports={ImportsCount}, refs={RefsCount}, lang={Lang}",
               code?.Length, extraImports?.Length ?? 0, extraRefs?.Length ?? 0, languageVersion);

        // Validate inputs
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentNullException(nameof(code), "VB.NET code cannot be null or whitespace.");
        extraImports ??= [];
        extraImports = extraImports.Concat(["System.Collections.Generic", "System.Linq", "System.Security.Claims"]).ToArray();

        // 🔧 1.  Build a real VB file around the user snippet
        string source = BuildWrappedSource(code, extraImports, vbReturnType: GetVbReturnType(typeof(TResult)),
            locals: locals);
        var startIndex = source.IndexOf(StartMarker);
        if (startIndex < 0)
            throw new ArgumentException($"VB.NET code must contain the marker '{StartMarker}' to indicate where user code starts.", nameof(code));
        int startLine = CcUtilities.GetLineNumber(source, startIndex);
        if (log.IsEnabled(LogEventLevel.Debug))
            log.Debug("VB.NET script starts at line {LineNumber}", startLine);

        // Parse the source code into a syntax tree
        // This will allow us to analyze and compile the code
        var tree = VisualBasicSyntaxTree.ParseText(
                       source,
                       new VisualBasicParseOptions(LanguageVersion.VisualBasic16));

        // 🔧 2.  References = everything already loaded  +  extras
        var refs = AppDomain.CurrentDomain.GetAssemblies()
                     .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
                     .Select(a => MetadataReference.CreateFromFile(a.Location))
                     .Concat(extraRefs?.Select(r => MetadataReference.CreateFromFile(r.Location))
                             ?? Enumerable.Empty<MetadataReference>())
                                .Append(MetadataReference.CreateFromFile(typeof(Microsoft.VisualBasic.Constants).Assembly.Location))
                                .Append(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))            // System.Private.CoreLib
                                .Append(MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location))        // System.Linq
                                .Append(MetadataReference.CreateFromFile(typeof(HttpContext).Assembly.Location))       // Microsoft.AspNetCore.Http
                                .Append(MetadataReference.CreateFromFile(typeof(Console).Assembly.Location))          // System.Console
                                .Append(MetadataReference.CreateFromFile(typeof(Serilog.Log).Assembly.Location))       // Serilog
                                .Append(MetadataReference.CreateFromFile(typeof(ClaimsPrincipal).Assembly.Location))    // System.Security.Claims
                                ;

        // 🔧 3.  Normal DLL compilation
        var compilation = VisualBasicCompilation.Create(
            assemblyName: $"RouteScript_{Guid.NewGuid():N}",
            syntaxTrees: [tree],
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
                // Log the errors with adjusted line numbers                
                log.Error($"VBNet script compilation completed with {errors.Length} error(s):");
                foreach (var error in errors)
                {
                    var location = error.Location.IsInSource
                        ? $" at line {error.Location.GetLineSpan().StartLinePosition.Line - startLine + 1}" // ⬅ adjust line number based on start
                        : "";
                    log.Error($"  Error [{error.Id}]: {error.GetMessage()}{location}");
                }
                // Throw an exception with the error details
                // This will stop the execution and provide feedback to the user
                throw new CompilationErrorException("VBNet route code compilation failed", emitResult.Diagnostics);
            }
        }
        // Log warnings if any (optional - for debugging)
        var warnings = emitResult.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Warning).ToArray();
        if (warnings is not null && warnings.Length != 0)
        {
            // Log the warnings with adjusted line numbers
            log.Warning($"VBNet script compilation completed with {warnings.Length} warning(s):");
            foreach (var warning in warnings)
            {
                var location = warning.Location.IsInSource
                    ? $" at line {warning.Location.GetLineSpan().StartLinePosition.Line - startLine + 1}" // ⬅ adjust line number based on start
                    : "";
                log.Warning($"  Warning [{warning.Id}]: {warning.GetMessage()}{location}");
            }
            // If there are warnings, log a debug message
            if (log.IsEnabled(LogEventLevel.Debug))
                log.Debug("VB.NET script compiled with warnings: {Count}", warnings.Length);
        }
        // If there are no warnings, log a debug message
        if (warnings != null && warnings.Length == 0 && log.IsEnabled(LogEventLevel.Debug))
            log.Debug("VB.NET script compiled successfully with no warnings.");
        // If there are no errors, log a debug message
        if (emitResult.Success && log.IsEnabled(LogEventLevel.Debug))
            log.Debug("VB.NET script compiled successfully with no errors.");


        // If there are no errors, proceed to load the assembly and create the delegate
        if (log.IsEnabled(LogEventLevel.Debug))
            log.Debug("VB.NET script compiled successfully, loading assembly...");
        ms.Position = 0;
        var asm = Assembly.Load(ms.ToArray());
        var runMethod = asm.GetType("RouteScript")!
                           .GetMethod("Run", BindingFlags.Public | BindingFlags.Static)!;

        // Build Func<CsGlobals, Task<TResult>> at runtime
        var delegateType = typeof(Func<,>).MakeGenericType(
                               typeof(CsGlobals),
                               typeof(Task<>).MakeGenericType(typeof(TResult)));

        return (Func<CsGlobals, Task<TResult>>)runMethod.CreateDelegate(delegateType);
    }

    private static string BuildWrappedSource(string? code, IEnumerable<string>? extraImports,
    string vbReturnType, IReadOnlyDictionary<string, object?>? locals = null
       )
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

        sb.AppendLine($"""
                Public Module RouteScript
                    Public Async Function Run(g As CsGlobals) As Task(Of {vbReturnType})
                        Await Task.Yield() ' placeholder await
                        Dim Request  = g.Context?.Request
                        Dim Response = g.Context?.Response
                        Dim Context  = g.Context
        """);

        // only emit these _when_ you called Compile with locals:
        if (locals?.ContainsKey("username") ?? false)
            sb.AppendLine("""
        ' only bind creds if someone passed them in 
                        Dim username As String = CStr(g.Locals("username"))                
        """);

        if (locals?.ContainsKey("password") ?? false)
            sb.AppendLine("""
                        Dim password As String = CStr(g.Locals("password"))
        """);

        if (locals?.ContainsKey("providedKey") == true)
            sb.AppendLine("""
        ' only bind keys if someone passed them in
                        Dim providedKey As String = CStr(g.Locals("providedKey"))
        """);
        if (locals?.ContainsKey("providedKeyBytes") == true)
            sb.AppendLine("""
                        Dim providedKeyBytes As Byte() = CType(g.Locals("providedKeyBytes"), Byte())
        """);

        if (locals?.ContainsKey("identity") == true)
            sb.AppendLine("""
                        Dim identity As String = CStr(g.Locals("identity"))
        """);

        // add the Marker for user code
        sb.AppendLine(StartMarker);
        // ---- User code starts here ----

        if (!string.IsNullOrEmpty(code))
        {
            // indent the user snippet so VB is happy
            sb.AppendLine(String.Join(
                Environment.NewLine,
                code.Split('\n').Select(l => "        " + l.TrimEnd('\r'))));
        }
        sb.AppendLine("""
                     
                End Function
            End Module
    """);
        return sb.ToString();
    }
}