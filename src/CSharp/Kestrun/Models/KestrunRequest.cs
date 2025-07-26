namespace Kestrun;

/// <summary>
/// Lightweight representation of an incoming HTTP request.
/// </summary>
public class KestrunRequest
{
    /// <summary>The HTTP verb used for the request.</summary>
    public required string Method { get; set; }

    /// <summary>The request path.</summary>
    public required string Path { get; set; }

    /// <summary>Parsed query string parameters.</summary>
    public required Dictionary<string, string> Query { get; set; }

    /// <summary>All request headers.</summary>
    public required Dictionary<string, string> Headers { get; set; }

    /// <summary>Raw request body text.</summary>
    public required string Body { get; set; }
    public string? Authorization { get; private set; }

    /// <summary>
    /// Create a <see cref="KestrunRequest"/> from an <see cref="HttpContext"/> by reading the body stream.
    /// </summary>
    public static async Task<KestrunRequest> NewRequest(HttpContext context)
    {
        using var reader = new StreamReader(context.Request.Body);
        var body = await reader.ReadToEndAsync();

        return new KestrunRequest
        {
            Method = context.Request.Method,
            Path = context.Request.Path.ToString(),
            Query = context.Request.Query.ToDictionary(x => x.Key, x => x.Value.ToString()),
            Headers = context.Request.Headers.ToDictionary(x => x.Key, x => x.Value.ToString()),
            Authorization = context.Request.Headers.Authorization.ToString(),
            // Note: Body is read as a string, not a byte array
            Body = body
        };
    }
}
