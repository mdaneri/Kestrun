namespace Kestrun.Models;

 
/// <summary>
/// Represents a request model for Kestrun, containing HTTP method, path, query, headers, body, authorization, cookies, and form data.
/// </summary>
public class KestrunRequest
{ 
/// <summary>
/// Gets or sets the HTTP method for the request (e.g., GET, POST).
/// </summary>
public required string Method { get; set; }
 
    /// <summary>
    /// Gets or sets the request path (e.g., "/api/resource").
    /// </summary>
    public required string Path { get; set; }
 
    /// <summary>
    /// Gets or sets the query parameters for the request as a dictionary of key-value pairs.
    /// </summary>
    public required Dictionary<string, string> Query { get; set; }

 
    /// <summary>
    /// Gets or sets the headers for the request as a dictionary of key-value pairs.
    /// </summary>
    public required Dictionary<string, string> Headers { get; set; }

 
    /// <summary>
    /// Gets or sets the body content of the request as a string.
    /// </summary>
    public required string Body { get; set; }
    
    /// <summary>
    /// Gets the authorization header value for the request, if present.
    /// </summary>
    public string? Authorization { get; private set; }
    
    /// <summary>
    /// Gets the cookies for the request as an <see cref="IRequestCookieCollection"/>, if present.
    /// </summary>
    public IRequestCookieCollection? Cookies { get; internal set; }

    /// <summary>
    /// Gets the form data for the request as a dictionary of key-value pairs, if present.
    /// </summary>
    public Dictionary<string, string>? Form { get; internal set; }

 
    /// <summary>
    /// Creates a new <see cref="KestrunRequest"/> instance from the specified <see cref="HttpContext"/>.
    /// </summary>
    /// <param name="context">The HTTP context containing the request information.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the constructed <see cref="KestrunRequest"/>.</returns>
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
            Cookies = context.Request.Cookies, // Assuming this is a dictionary-like object
            Form = context.Request.HasFormContentType ? context.Request.Form.ToDictionary(x => x.Key, x => x.Value.ToString()) : [],
            // Note: Body is read as a string, not a byte array
            Body = body
        };
    }
}
