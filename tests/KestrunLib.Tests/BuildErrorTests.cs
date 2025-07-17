using KestrumLib;
using System.Management.Automation;
using Microsoft.AspNetCore.Http;
using System.IO;
using System.Threading.Tasks;
using Xunit;

public class BuildErrorTests
{
    [Fact]
    public void Text_ReturnsFormattedErrors()
    {
        using PowerShell ps = PowerShell.Create();
        ps.AddScript("Write-Error 'boom'");
        ps.Invoke();
        string text = BuildError.Text(ps);
        Assert.Contains("boom", text);
    }

    [Fact]
    public async Task ResponseAsync_WritesToContext()
    {
        using PowerShell ps = PowerShell.Create();
        ps.AddScript("Write-Error 'oops'");
        ps.Invoke();
        var ctx = new DefaultHttpContext();
        await BuildError.ResponseAsync(ctx, ps);
        ctx.Response.Body.Seek(0, System.IO.SeekOrigin.Begin);
        string body = new StreamReader(ctx.Response.Body).ReadToEnd();
        Assert.Contains("oops", body);
    }
}
