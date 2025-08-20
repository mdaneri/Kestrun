using System.Management.Automation;
using Microsoft.AspNetCore.Http;
using Xunit;
using Kestrun.Utilities;

namespace KestrunTests.Utility;
public class BuildErrorTests
{
    [Fact]
    public void Text_ReturnsFormattedErrors()
    {
        using PowerShell ps = PowerShell.Create();
        _ = ps.AddScript("Write-Error 'boom'");
        _ = ps.Invoke();
        string text = BuildError.Text(ps);
        Assert.Contains("boom", text);
    }

    [Fact]
    public async Task ResponseAsync_WritesToContext()
    {
        using PowerShell ps = PowerShell.Create();
        _ = ps.AddScript("Write-Error 'oops'");
        _ = ps.Invoke();
        var ctx = new DefaultHttpContext();
        ctx.Response.Body = new MemoryStream();
        await BuildError.ResponseAsync(ctx, ps);
        ctx.Response.Body.Position = 0;
        string body = new StreamReader(ctx.Response.Body).ReadToEnd();
        Assert.Contains("oops", body);
    }

    [Fact]
    public void Result_ReturnsIResult()
    {
        using PowerShell ps = PowerShell.Create();
        _ = ps.AddScript("Write-Error 'uh'");
        _ = ps.Invoke();
        var result = BuildError.Result(ps);
        Assert.NotNull(result);
    }
}
