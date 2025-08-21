using Kestrun.Models;
using Microsoft.AspNetCore.Http;
using System.Text;
using Xunit;

namespace KestrunTests.Models;

public class KestrunRequestTests
{
    private static DefaultHttpContext MakeContext(
        string method = "POST",
        string path = "/api/test",
        string? body = "data")
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Method = method;
        ctx.Request.Path = path;
        ctx.Request.Headers["Authorization"] = "Bearer token";
        ctx.Request.QueryString = new QueryString("?a=1&b=two");
        if (body != null)
        {
            var bytes = Encoding.UTF8.GetBytes(body);
            ctx.Request.Body = new MemoryStream(bytes);
        }
        return ctx;
    }

    [Fact]
    public async Task NewRequest_Reads_Body_And_Headers_And_Query()
    {
        var ctx = MakeContext();
        var req = await KestrunRequest.NewRequest(ctx);

        Assert.Equal("POST", req.Method);
        Assert.Equal("/api/test", req.Path);
        Assert.Equal("1", req.Query["a"]);
        Assert.Equal("two", req.Query["b"]);
        Assert.Equal("Bearer token", req.Authorization);
        Assert.Equal("data", req.Body);
    }

    [Fact]
    public async Task NewRequest_Reads_Form_When_HasFormContentType()
    {
        var ctx = MakeContext(body: null);
        ctx.Request.Method = "POST";
        ctx.Request.ContentType = "application/x-www-form-urlencoded";
        var formBody = "foo=bar&baz=1&baz=2";
        ctx.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(formBody));

        var req = await KestrunRequest.NewRequest(ctx);

        Assert.NotNull(req.Form);
        Assert.Equal("bar", req.Form!["foo"]);
        Assert.Equal("1,2", req.Form!["baz"]);
    }
}
