using Kestrun.Languages;
using Kestrun.Hosting;
using Kestrun.Models;
using Microsoft.AspNetCore.Http;
using Moq;
using Serilog;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using Xunit;

namespace KestrunTests.Languages;

public class PowerShellDelegateBuilderTests
{
    private static (DefaultHttpContext http, KestrunContext krContext) MakeContext()
    {
        var http = new DefaultHttpContext();
        http.Request.Method = "GET";
        http.Request.Path = "/";

        // Build Kestrun context
        var req = new KestrunRequest
        {
            Method = http.Request.Method,
            Path = http.Request.Path,
            Query = new(),
            Headers = new(),
            Body = string.Empty
        };
        var res = new KestrunResponse(req);
        var kr = new KestrunContext(req, res, http);
        http.Items[PowerShellDelegateBuilder.KR_CONTEXT_KEY] = kr;
        return (http, kr);
    }

    [Fact]
    public async Task Build_ExecutesScript_AndAppliesDefaultResponse()
    {
        var log = new Mock<ILogger>(MockBehavior.Loose).Object;
        var (http, kr) = MakeContext();

        // Prepare PowerShell with an open runspace
        using var runspace = RunspaceFactory.CreateRunspace();
        runspace.Open();
        using var ps = PowerShell.Create();
        ps.Runspace = runspace;
        http.Items[PowerShellDelegateBuilder.PS_INSTANCE_KEY] = ps;

        // trivial script
        var code = "$x = 1; $x | Out-Null";
        var del = PowerShellDelegateBuilder.Build(code, log, arguments: null);

        await del(http);

        Assert.Equal(200, http.Response.StatusCode);
        Assert.Equal("text/plain; charset=utf-8", http.Response.ContentType);
    }
}
