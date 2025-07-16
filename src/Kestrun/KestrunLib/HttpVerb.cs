namespace KestrumLib
{
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
        public static string ToMethodString(this HttpVerb v) => v.ToString().ToUpperInvariant();
    }
}