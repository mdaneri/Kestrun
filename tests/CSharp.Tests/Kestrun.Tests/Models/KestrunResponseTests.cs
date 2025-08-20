using Microsoft.AspNetCore.Http;
using Xunit;
using Kestrun.Models;

namespace KestrunTests.Models;
public class KestrunResponseTests
{
    private static KestrunResponse NewRes() =>
        new(new KestrunRequest
        {
            Method = "GET",
            Path = "/",
            Query = [],
            Headers = [],
            Body = string.Empty
        });

    [Fact]
    public void WriteTextResponse_SetsFields()
    {
        var res = NewRes();
        res.WriteTextResponse("hello", StatusCodes.Status200OK);
        Assert.Equal("hello", res.Body);
        Assert.Equal(StatusCodes.Status200OK, res.StatusCode);
        Assert.Contains("text/plain", res.ContentType);
    }

    [Fact]
    public void WriteJsonResponse_SetsFields()
    {
        var res = NewRes();
        res.WriteJsonResponse(new { a = 1 });
        Assert.Contains("\"a\": 1", res.Body as string);
        Assert.Contains("application/json", res.ContentType);
    }

    [Fact]
    public void WriteYamlResponse_SetsFields()
    {
        var res = NewRes();
        res.WriteYamlResponse(new { a = 1 });
        Assert.Contains("a: 1", res.Body as string);
        Assert.Contains("application/yaml", res.ContentType);
    }

    [Fact]
    public void WriteXmlResponse_SetsFields()
    {
        var res = NewRes();
        res.WriteXmlResponse(new { a = 1 });
        Assert.Contains("<a>1</a>", res.Body as string);
        Assert.Contains("application/xml", res.ContentType);
    }

    [Fact]
    public void WriteBinaryResponse_SetsFields()
    {
        var res = NewRes();
        res.WriteBinaryResponse([1, 2, 3]);
        Assert.Equal(new byte[] { 1, 2, 3 }, res.Body as byte[]);
        Assert.Equal("application/octet-stream", res.ContentType);
    }

    [Fact]
    public void WriteStreamResponse_SetsFields()
    {
        var res = NewRes();
        using var ms = new MemoryStream([1, 2]);
        res.WriteStreamResponse(ms);
        Assert.Equal(ms, res.Body);
        Assert.Equal("application/octet-stream", res.ContentType);
    }

    [Fact]
    public void WriteRedirectResponse_SetsHeaders()
    {
        var res = NewRes();
        res.WriteRedirectResponse("/foo", "go");
        Assert.Equal("/foo", res.RedirectUrl);
        Assert.Equal("go", res.Body);
        Assert.Equal("/foo", res.Headers["Location"]);
    }

    [Fact]
    public void WriteErrorResponse_FromMessage()
    {
        var res = NewRes();
        res.WriteErrorResponse("oops");
        Assert.Equal(StatusCodes.Status500InternalServerError, res.StatusCode);
        Assert.NotNull(res.Body);
    }

    [Fact]
    public void WriteErrorResponse_FromException()
    {
        var res = NewRes();
        res.WriteErrorResponse(new System.Exception("bad"));
        Assert.Equal(StatusCodes.Status500InternalServerError, res.StatusCode);
        Assert.NotNull(res.Body);
    }

    [Fact]
    public async Task ApplyTo_WritesHttpResponse()
    {
        var res = NewRes();
        res.WriteTextResponse("hi");
        var ctx = new DefaultHttpContext();
        ctx.Response.Body = new MemoryStream();
        await res.ApplyTo(ctx.Response);
        ctx.Response.Body.Position = 0;
        var text = new StreamReader(ctx.Response.Body).ReadToEnd();
        Assert.Equal("hi", text);
        Assert.Equal("text/plain; charset=utf-8", ctx.Response.ContentType);
    }

    [Theory]
    [InlineData("text/plain", true)]
    [InlineData("application/json", true)]
    [InlineData("application/octet-stream", false)]
    public void IsTextBasedContentType_Works(string type, bool expected) => Assert.Equal(expected, KestrunResponse.IsTextBasedContentType(type));
}
