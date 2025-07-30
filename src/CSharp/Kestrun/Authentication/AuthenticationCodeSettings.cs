using System.Reflection;
using Kestrun.Scripting;

namespace Kestrun.Authentication;

public record AuthenticationCodeSettings
{
    public ScriptLanguage Language { get; init; } = ScriptLanguage.Native;
    public string? Code { get; init; }
    public string[]? ExtraImports { get; init; }
    public Assembly[]? ExtraRefs { get; init; }
     public AuthenticationCodeSettings Copy()
    {
        return this with
        {
            ExtraImports = ExtraImports is not null ? [.. ExtraImports] : null,
            ExtraRefs = ExtraRefs is not null ? [.. ExtraRefs] : null
        };
    }
}