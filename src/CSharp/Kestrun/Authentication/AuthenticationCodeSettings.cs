using System.Reflection;
using Kestrun.Scripting;

namespace Kestrun.Authentication;

/// <summary>
/// Represents the settings for authentication code, including language, code, extra imports, and references.
/// </summary>
public record AuthenticationCodeSettings
{
    /// <summary>
    /// Gets the scripting language used for authentication code.
    /// </summary>
    public ScriptLanguage Language { get; init; } = ScriptLanguage.Native;
    /// <summary>
    /// Gets the authentication code as a string.
    /// </summary>
    public string? Code { get; init; }
    /// <summary>
    /// Gets the extra import namespaces required for authentication code.
    /// </summary>
    public string[]? ExtraImports { get; init; }
    /// <summary>
    /// Gets the extra assembly references required for authentication code.
    /// </summary>
    public Assembly[]? ExtraRefs { get; init; }
}