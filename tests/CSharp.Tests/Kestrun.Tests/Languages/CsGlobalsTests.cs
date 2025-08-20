using Kestrun.Hosting;
using Kestrun.Languages;
using Kestrun.Models;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace KestrunTests.Languages;

public class CsGlobalsTests
{
    [Fact]
    public void Ctor_WithGlobals_SetsProperties()
    {
        var g = new Dictionary<string, object?> { ["a"] = "b" };
        var globals = new CsGlobals(g);
        Assert.Same(g, globals.Globals);
        Assert.NotNull(globals.Locals);
        Assert.Null(globals.Context);
    }

    [Fact]
    public void Ctor_WithGlobalsAndContext_SetsContext()
    {
        var g = new Dictionary<string, object?>();
        var http = new DefaultHttpContext();
        var req = new KestrunRequest
        {
            Method = "GET",
            Path = "/",
            Query = [],
            Headers = [],
            Body = string.Empty
        };
        var res = new KestrunResponse(req);
        var ctx = new KestrunContext(req, res, http);

        var globals = new CsGlobals(g, ctx);
        Assert.Same(ctx, globals.Context);
    }

    [Fact]
    public void Ctor_WithGlobalsContextAndLocals_SetsAll()
    {
        var g = new Dictionary<string, object?>();
        var l = new Dictionary<string, object?> { ["x"] = 1 };
        var http = new DefaultHttpContext();
        var req = new KestrunRequest
        {
            Method = "GET",
            Path = "/",
            Query = [],
            Headers = [],
            Body = string.Empty
        };
        var res = new KestrunResponse(req);
        var ctx = new KestrunContext(req, res, http);

        var globals = new CsGlobals(g, ctx, l);
        Assert.Same(g, globals.Globals);
        Assert.Same(l, globals.Locals);
        Assert.Same(ctx, globals.Context);
    }
}
