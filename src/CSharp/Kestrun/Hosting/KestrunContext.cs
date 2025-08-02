

using System.Security.Claims;
using Kestrun.Models;

namespace Kestrun.Hosting;

/// <summary>
/// Represents the context for a Kestrun request, including the request, response, and HTTP context.
/// </summary>
/// <param name="Request">The Kestrun request.</param>
/// <param name="Response">The Kestrun response.</param>
/// <param name="HttpContext">The associated HTTP context.</param>
public sealed record KestrunContext(KestrunRequest Request, KestrunResponse Response, HttpContext HttpContext)
{
    // handy shortcuts
    /// <summary>
    /// Gets the session associated with the current HTTP context.
    /// </summary>
    public ISession Session => HttpContext.Session;
    /// <summary>
    /// Gets the cancellation token that is triggered when the HTTP request is aborted.
    /// </summary>
    public CancellationToken Ct => HttpContext.RequestAborted;
    /// <summary>
    /// Gets the collection of key/value pairs associated with the current HTTP context.
    /// </summary>
    public IDictionary<object, object?> Items => HttpContext.Items;

    /// <summary>
    /// Gets the user associated with the current HTTP context.
    /// </summary>
    public ClaimsPrincipal User => HttpContext.User;
}
