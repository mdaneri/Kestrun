using Kestrun.SharedState;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Scripting;
using Serilog.Events;
using System.Reflection;
using System.Text;
using static Kestrun.KestrunHost;

namespace Kestrun.Scheduling;

internal static class RoslynJobFactory
{
    public static Func<CancellationToken, Task> Build(
        string code,
        Serilog.ILogger log,
        string[]? extraImports,
        Assembly[]? extraRefs,
        LanguageVersion langVer = LanguageVersion.CSharp12)
    {
        if (log.IsEnabled(LogEventLevel.Debug))
            log.Debug("Building C# job, code length={Length}, imports={ImportsCount}, refs={RefsCount}, lang={Lang}",
                code?.Length, extraImports?.Length ?? 0, extraRefs?.Length ?? 0, langVer);
        /* 1️⃣  Build ScriptOptions once */
        var opts = ScriptOptions.Default
                                .WithImports("System", "System.Linq",
                                             "System.Threading.Tasks",
                                             "Microsoft.AspNetCore.Http")
                                .WithReferences(typeof(HttpContext).Assembly,
                                                typeof(KestrunResponse).Assembly)
                                .WithLanguageVersion(langVer);
        if (log.IsEnabled(LogEventLevel.Debug))
            log.Debug("C# job options: {@Options}", opts);
        if (extraImports is { Length: > 0 })
            opts = opts.WithImports(opts.Imports.Concat(extraImports));
        if (extraRefs is { Length: > 0 })
            opts = opts.WithReferences(opts.MetadataReferences
                                        .Concat(extraRefs.Select(r =>
                                            MetadataReference.CreateFromFile(r.Location))));

        /* 2️⃣  ︎OPTIONAL: keep the alias-generation – it only runs once now */
        var globalsStub = SharedStateStore.Snapshot();
        if (globalsStub.Count > 0)
        {
            var sb = new StringBuilder();
            foreach (var (name, value) in globalsStub)
                sb.AppendLine($"var {name} = " +
                              $"({value?.GetType().FullName ?? "object"})" +
                              $"Globals[\"{name}\"];");
            code = sb + code;
        }
        if (log.IsEnabled(LogEventLevel.Debug))
            log.Debug("C# job code after alias generation: {Code}", code);

        /* 3️⃣  Compile once */
        var script = CSharpScript.Create(code, opts, typeof(CsGlobals));
        var diagnostics = script.Compile();
        if (diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error))
            throw new CompilationErrorException("C# job failed to compile", diagnostics);
        if (log.IsEnabled(LogEventLevel.Debug))
            log.Debug("C# job compiled successfully, diagnostics: {@Diagnostics}", diagnostics);
        /* 4️⃣  Turn it into a runner we can call repeatedly */
        var runner = script.CreateDelegate();   // returns ScriptRunner<object?>
        if (log.IsEnabled(LogEventLevel.Debug))
            log.Debug("C# job runner created, type={Type}", runner.GetType());
        /* 5️⃣  Returned delegate = *execute only* */
        return async ct =>
        {
            if (log.IsEnabled(LogEventLevel.Debug))
                log.Debug("Executing C# job at {Now:O}", DateTimeOffset.UtcNow);
            var globals = new CsGlobals(SharedStateStore.Snapshot());
            await runner(globals, ct).ConfigureAwait(false);
        };
    }
}


