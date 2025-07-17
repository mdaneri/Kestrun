namespace KestrumLib
{
    /// <summary>
    /// HTTP methods supported by Kestrun.
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
        /// Converts the <see cref="HttpVerb"/> to its uppercase string representation.
        /// </summary>
        /// <param name="v">The verb to convert.</param>
        /// <returns>Uppercase string form of the verb.</returns>
        public static string ToMethodString(this HttpVerb v) => v.ToString().ToUpperInvariant();
    }
}