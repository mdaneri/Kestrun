namespace Kestrun.Utilities;

/// <summary>
/// Common HTTP verbs recognized by the framework.
/// </summary>
/// <remarks>
/// This enum includes standard HTTP methods as well as WebDAV extensions.
/// It is designed to be extensible for future HTTP methods. 
/// The enum values correspond to the HTTP methods as defined in various RFCs:
/// RFC 4918 - HTTP Extensions for WebDAV
/// RFC 3744 - WebDAV Access Control Protocol
/// RFC 3253 - Versioning Extensions to WebDAV
/// RFC 5323 - WebDAV SEARCH
/// RFC 5842 - WebDAV Ordered Collections
/// RFC 5689 - WebDAV Bindings
/// RFC 6620 - WebDAV MERGE
/// RFC 5689 - WebDAV BIND
/// RFC 4918 - WebDAV
/// RFC 7231 - HTTP/1.1 Semantics and Content
/// RFC 7232 - HTTP/1.1 Conditional Requests
/// RFC 7233 - HTTP/1.1 Range Requests
/// RFC 7234 - HTTP/1.1 Caching
/// RFC 7235 - HTTP/1.1 Authentication
/// </remarks> 
[Flags]
public enum HttpVerb
{
    /// <summary>
    /// Represents the HTTP GET method.
    /// </summary>
    Get,
    /// <summary>
    /// Represents the HTTP HEAD method.
    /// </summary>
    Head,
    /// <summary>
    /// Represents the HTTP POST method.
    /// </summary>
    Post,
    /// <summary>
    /// Represents the HTTP PUT method.
    /// </summary>
    Put,
    /// <summary>
    /// Represents the HTTP PATCH method.
    /// </summary>
    Patch,
    /// <summary>
    /// Represents the HTTP DELETE method.
    /// </summary>
    Delete,
    /// <summary>
    /// Represents the HTTP OPTIONS method.
    /// </summary>
    Options,
    /// <summary>
    /// Represents the HTTP TRACE method.
    /// </summary>
    Trace,
    // WebDAV verbs
    /// <summary>
    /// Represents the HTTP PROPFIND method (WebDAV).
    /// </summary>
    PropFind,
    /// <summary>
    /// Represents the HTTP PROPPATCH method (WebDAV).
    /// </summary>
    PropPatch,
    /// <summary>
    /// Represents the HTTP MKCOL method (WebDAV).
    /// </summary>
    MkCol,
    /// <summary>
    /// Represents the HTTP COPY method (WebDAV).
    /// </summary>
    Copy,
    /// <summary>
    /// Represents the HTTP MOVE method (WebDAV).
    /// </summary>
    Move,
    /// <summary>
    /// Represents the HTTP LOCK method (WebDAV).
    /// </summary>
    Lock,
    /// <summary>
    /// Represents the HTTP UNLOCK method (WebDAV).
    /// </summary>
    Unlock,
    /// <summary>
    /// Represents the HTTP REPORT method (WebDAV).
    /// </summary>
    Report,
    /// <summary>
    /// Represents the HTTP ACL method (WebDAV).
    /// </summary>
    Acl,
    /// <summary>
    /// Represents the HTTP SEARCH method (WebDAV).
    /// </summary>
    Search,
    /// <summary>
    /// Represents the HTTP MERGE method (WebDAV).
    /// </summary>
    Merge,
    /// <summary>
    /// Represents the HTTP BIND method (WebDAV).
    /// </summary>
    Bind,
    /// <summary>
    /// Represents the HTTP UNBIND method (WebDAV).
    /// </summary>
    Unbind,
    /// <summary>
    /// Represents the HTTP REBIND method (WebDAV).
    /// </summary>
    Rebind,
    /// <summary>
    /// Represents the HTTP UPDATE method (WebDAV).
    /// </summary>
    Update,
    /// <summary>
    /// Represents the HTTP VERSION-CONTROL method (WebDAV).
    /// </summary>
    VersionControl,
    /// <summary>
    /// Represents the HTTP CHECKIN method (WebDAV).
    /// </summary>
    Checkin,
    /// <summary>
    /// Represents the HTTP CHECKOUT method (WebDAV).
    /// </summary>
    Checkout,
    /// <summary>
    /// Represents the HTTP UNCHECKOUT method (WebDAV).
    /// </summary>
    Uncheckout,
    /// <summary>
    /// Represents the HTTP MKWORKSPACE method (WebDAV).
    /// </summary>
    MkWorkspace,
    /// <summary>
    /// Represents the HTTP LABEL method (WebDAV).
    /// </summary>
    Label,
    /// <summary>
    /// Represents the HTTP ORDERPATCH method (WebDAV).
    /// </summary>
    OrderPatch
}

/// <summary>
/// Extension methods for the <see cref="HttpVerb"/> enum.
/// </summary>
public static class HttpVerbExtensions
{
    /// <summary>
    /// Convert the verb enum to its uppercase HTTP method string.
    /// </summary>
    public static string ToMethodString(this HttpVerb v) => v.ToString().ToUpperInvariant();

    /// <summary>
    /// Convert a HTTP method string to its corresponding HttpVerb enum value.
    /// </summary>
    /// <param name="method">The HTTP method string (case-insensitive).</param>
    /// <returns>The corresponding HttpVerb enum value.</returns>
    /// <exception cref="ArgumentException">Thrown when the method string is not recognized.</exception>
    public static HttpVerb FromMethodString(string method)
    {
        if (string.IsNullOrWhiteSpace(method))
            throw new ArgumentException("Method cannot be null or whitespace.", nameof(method));

        // Handle special cases where enum names don't match HTTP method strings
        var normalizedMethod = method.Trim().ToUpperInvariant() switch
        {
            "PROPFIND" => "PropFind",
            "PROPPATCH" => "PropPatch",
            "MKCOL" => "MkCol",
            "VERSION-CONTROL" => "VersionControl",
            "MKWORKSPACE" => "MkWorkspace",
            "ORDERPATCH" => "OrderPatch",
            _ => method.Trim()
        };

        if (Enum.TryParse<HttpVerb>(normalizedMethod, true, out var result))
            return result;

        throw new ArgumentException($"Unknown HTTP method: {method}", nameof(method));
    }

    /// <summary>
    /// Try to convert a HTTP method string to its corresponding HttpVerb enum value.
    /// </summary>
    /// <param name="method">The HTTP method string (case-insensitive).</param>
    /// <param name="verb">When this method returns, contains the HttpVerb value if conversion succeeded, or default value if it failed.</param>
    /// <returns>true if the conversion succeeded; otherwise, false.</returns>
    public static bool TryFromMethodString(string method, out HttpVerb verb)
    {
        verb = default;
        
        if (string.IsNullOrWhiteSpace(method))
            return false;

        try
        {
            verb = FromMethodString(method);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}