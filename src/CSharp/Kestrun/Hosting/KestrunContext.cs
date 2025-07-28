

namespace Kestrun.Hosting;

public sealed record KestrunContext(
    KestrunRequest Request,
    KestrunResponse Response,
    HttpContext HttpContext)
{
    // handy shortcuts
    public ISession Session => HttpContext.Session;
    public CancellationToken Ct => HttpContext.RequestAborted;
    public IDictionary<object, object?> Items => HttpContext.Items;
}
