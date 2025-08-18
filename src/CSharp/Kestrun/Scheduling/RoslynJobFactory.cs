using Kestrun.Languages;
using Kestrun.Models;
using Kestrun.SharedState;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Scripting;
using Serilog.Events;
using System.Reflection;
using System.Text;

namespace Kestrun.Scheduling;

internal static class RoslynJobFactory
{
    public static Func<CancellationToken, Task> Build(
        string code,
        Serilog.ILogger log,
        string[]? extraImports,
        Assembly[]? extraRefs,
        IReadOnlyDictionary<string, object?>? locals,
        LanguageVersion languageVersion = LanguageVersion.CSharp12)
    {
        if (log.IsEnabled(LogEventLevel.Debug))
        {
            log.Debug("Building C# job, code length={Length}, imports={ImportsCount}, refs={RefsCount}, lang={Lang}",
                code?.Length, extraImports?.Length ?? 0, extraRefs?.Length ?? 0, languageVersion);
        }

        var script = CSharpDelegateBuilder.Compile(code: code, log: log, extraImports: extraImports, extraRefs: extraRefs, locals: locals, languageVersion: languageVersion);
        var runner = script.CreateDelegate();   // returns ScriptRunner<object?>
        if (log.IsEnabled(LogEventLevel.Debug))
        {
            log.Debug("C# job runner created, type={Type}", runner.GetType());
        }
        /* 5️⃣  Returned delegate = *execute only* */
        return async ct =>
        {
            if (log.IsEnabled(LogEventLevel.Debug))
            {
                log.Debug("Executing C# job at {Now:O}", DateTimeOffset.UtcNow);
            }

            var globals = new CsGlobals(SharedStateStore.Snapshot());
            await runner(globals, ct).ConfigureAwait(false);
        };
    }



    public static Func<CancellationToken, Task> Build(
       string code,
       Serilog.ILogger log,
       string[]? extraImports,
       Assembly[]? extraRefs,
       IReadOnlyDictionary<string, object?>? locals,
       Microsoft.CodeAnalysis.VisualBasic.LanguageVersion languageVersion = Microsoft.CodeAnalysis.VisualBasic.LanguageVersion.VisualBasic16_9)
    {
        if (log.IsEnabled(LogEventLevel.Debug))
        {
            log.Debug("Building C# job, code length={Length}, imports={ImportsCount}, refs={RefsCount}, lang={Lang}",
                code?.Length, extraImports?.Length ?? 0, extraRefs?.Length ?? 0, languageVersion);
        }

        var script = VBNetDelegateBuilder.Compile<object>(code:code, log:log, extraImports:extraImports, extraRefs:extraRefs, locals:locals, languageVersion:languageVersion);

        return async ct =>
        {
            if (log.IsEnabled(LogEventLevel.Debug))
            {
                log.Debug("Executing C# job at {Now:O}", DateTimeOffset.UtcNow);
            }

            var globals = new CsGlobals(SharedStateStore.Snapshot());
            await script(globals).ConfigureAwait(false);
        };
    }
}


