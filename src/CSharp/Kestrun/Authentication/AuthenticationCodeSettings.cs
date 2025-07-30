using System.Reflection;
using Kestrun.Scripting;

namespace Kestrun.Authentication;
public record AuthenticationCodeSettings
{
    public ScriptLanguage Language { get; init; } = ScriptLanguage.Native;
    public string? Code { get; init; }
    public string[]? ExtraImports { get; init; }
    public Assembly[]? ExtraRefs { get; init; }
}