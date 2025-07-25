namespace Kestrun.Utilities;

/// <summary>
/// Common HTTP verbs recognized by the framework.
/// </summary>
public enum HttpVerb
{
    Get,
    Head,
    Post,
    Put,
    Patch,
    Delete,
    Options,
    Trace
    // add less common ones if you need them
}

public static class HttpVerbExtensions
{
    /// <summary>
    /// Convert the verb enum to its uppercase HTTP method string.
    /// </summary>
    public static string ToMethodString(this HttpVerb v) => v.ToString().ToUpperInvariant();
}
