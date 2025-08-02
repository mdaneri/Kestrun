using System.Text;

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
        // ① Allow the body to be read multiple times
        context.Request.EnableBuffering();

        // ② Read the raw body into a string, then rewind
        string body;
        using (var reader = new StreamReader(
                   context.Request.Body,
                   encoding: Encoding.UTF8,
                   detectEncodingFromByteOrderMarks: false,
                   leaveOpen: true))
        {
            body = await reader.ReadToEndAsync();
            context.Request.Body.Position = 0;
        }

        // ③ If it's a form, read it safely
        var formDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (context.Request.HasFormContentType)
        {
            var form = await context.Request.ReadFormAsync();
            foreach (var kv in form)
            {
                formDict[kv.Key] = kv.Value.ToString();
            }
        }

        return new KestrunRequest
        {
            Method = context.Request.Method,
            Path = context.Request.Path.ToString(),
            Query = context.Request.Query
                                  .ToDictionary(x => x.Key, x => x.Value.ToString()),
            Headers = context.Request.Headers
                                  .ToDictionary(x => x.Key, x => x.Value.ToString()),
            Authorization = context.Request.Headers.Authorization.ToString(),
            Cookies = context.Request.Cookies,
            Form = formDict,
            Body = body
        };
    }

}
