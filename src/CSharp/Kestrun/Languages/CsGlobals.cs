

using Kestrun.Hosting;

namespace Kestrun.Languages;
// ---------------------------------------------------------------------------
//  C# delegate builder  –  now takes optional imports / references
// --------------------------------------------------------------------------- 
public record CsGlobals
{

    public CsGlobals(IReadOnlyDictionary<string, object?> globals)
    {
        Globals = globals;
        Locals = new Dictionary<string, object?>();
        Context = null;
    }

    public CsGlobals(IReadOnlyDictionary<string, object?> globals, KestrunContext krcontext)
    {
        Globals = globals;
        Context = krcontext;
        Locals = new Dictionary<string, object?>();

    }

    public CsGlobals(IReadOnlyDictionary<string, object?> globals, KestrunContext krcontext, IReadOnlyDictionary<string, object?> locals)
    {
        Globals = globals;
        Context = krcontext;
        Locals = locals;
    }

    public IReadOnlyDictionary<string, object?> Globals { get; }

    public IReadOnlyDictionary<string, object?> Locals { get; }

    public KestrunContext? Context { get; }
}
