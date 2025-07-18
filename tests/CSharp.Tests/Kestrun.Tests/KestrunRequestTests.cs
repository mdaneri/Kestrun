using Kestrun;
using Microsoft.AspNetCore.Http;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Xunit;

public class KestrunRequestTests
{
    [Fact]
    public async Task NewRequest_ReadsContext()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Method = "POST";
        ctx.Request.Path = "/foo";
        ctx.Request.QueryString = new QueryString("?a=1");
        ctx.Request.Headers["X-Test"] = "yes";
        var bodyBytes = Encoding.UTF8.GetBytes("data");
        ctx.Request.Body = new MemoryStream(bodyBytes);

        var req = await KestrunRequest.NewRequest(ctx);
        Assert.Equal("POST", req.Method);
        Assert.Equal("/foo", req.Path);
        Assert.Equal("1", req.Query["a"]);
        Assert.Equal("yes", req.Headers["X-Test"]);
        Assert.Equal("data", req.Body);
    }
}
