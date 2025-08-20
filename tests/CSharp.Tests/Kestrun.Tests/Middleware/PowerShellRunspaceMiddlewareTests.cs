using Kestrun.Middleware;
using Kestrun.Scripting;
using Kestrun.Languages;
using Kestrun.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System.Management.Automation;
using Xunit;

namespace KestrunTests.Middleware;

public class PowerShellRunspaceMiddlewareTests
{
    [Fact]
    public async Task Middleware_InsertsPowerShellAndKestrunContext_AndCleansUp()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var app = new ApplicationBuilder(services);

        // Runspace pool: small for test
        var pool = new KestrunRunspacePoolManager(minRunspaces: 1, maxRunspaces: 1);

        // Use extension (covers both middleware and extension path)
        app.UsePowerShellRunspace(pool);

        // Terminal delegate: assert items are present during request
        app.Run(async ctx =>
        {
            Assert.True(ctx.Items.ContainsKey(PowerShellDelegateBuilder.PS_INSTANCE_KEY));
            Assert.True(ctx.Items.ContainsKey(PowerShellDelegateBuilder.KR_CONTEXT_KEY));

            var ps = Assert.IsType<PowerShell>(ctx.Items[PowerShellDelegateBuilder.PS_INSTANCE_KEY]);
            Assert.NotNull(ps.Runspace);
            Assert.True(ps.Runspace.RunspaceStateInfo.State is System.Management.Automation.Runspaces.RunspaceState.Opened);

            var kr = Assert.IsType<Kestrun.Hosting.KestrunContext>(ctx.Items[PowerShellDelegateBuilder.KR_CONTEXT_KEY]!);
            Assert.Equal(ctx, kr.HttpContext);

            // Verify session state variable set
            var ctxVar = ps.Runspace.SessionStateProxy.GetVariable("Context");
            Assert.Same(kr, ctxVar);

            // A simple write to ensure response is writable
            ctx.Response.StatusCode = 200;
            await ctx.Response.WriteAsync("");
        });

        var pipeline = app.Build();
        var http = new DefaultHttpContext();
        http.Request.Path = "/test";
        await pipeline(http);

        // After pipeline, PS instance should be removed (disposed and returned to pool)
        Assert.False(http.Items.ContainsKey(PowerShellDelegateBuilder.PS_INSTANCE_KEY));
        Assert.True(http.Items.ContainsKey(PowerShellDelegateBuilder.KR_CONTEXT_KEY));
    }
}
