using Serilog;
using Serilog.Events;

namespace Kestrun.Languages;
// ---------------------------------------------------------------------------
//  Python delegate builder  –  now takes optional imports / references
// ---------------------------------------------------------------------------

internal static class FSharpDelegateBuilder
{
    internal static RequestDelegate Build(string code, Serilog.ILogger logger)
    {
        // F# scripting not implemented yet
        if (Log.IsEnabled(LogEventLevel.Debug))
            Log.Debug("Building F# delegate, script length={Length}", code?.Length);
        throw new NotImplementedException("F# scripting is not yet supported in Kestrun.");
    }
}