namespace KestrumLib
{
    /// <summary>
    /// Languages supported for route handlers.
    /// </summary>
    public enum ScriptLanguage
    {
        /// <summary>PowerShell scripting language.</summary>
        PowerShell,
        /// <summary>C# scripting.</summary>
        CSharp,
        /// <summary>F# scripting (not yet implemented).</summary>
        FSharp,
        /// <summary>Python scripting via Python.NET.</summary>
        Python,
        /// <summary>JavaScript using ClearScript or Jint.</summary>
        JavaScript
    }
}