namespace Kestrun;

/// <summary>
/// Supported scripting languages for the Kestrun runtime.
/// </summary>
public enum ScriptLanguage
{
    Native,          // No scripting, just native C# code
    PowerShell,
    CSharp,
    FSharp,
    Python,
    JavaScript        // optional – ClearScript/Jint
}
