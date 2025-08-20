using Kestrun.Languages;
using Kestrun.SharedState;
using Microsoft.AspNetCore.Http;
using Serilog;
using Xunit;
using Moq;

namespace KestrunTests.Languages;

public class CSharpDelegateBuilderTests
{
    [Fact]
    public void Compile_ThrowsOnNullOrWhitespace()
    {
        var log = new Mock<ILogger>(MockBehavior.Loose).Object;
        Assert.Throws<ArgumentNullException>(() => CSharpDelegateBuilder.Compile(null, log, null, null, null));
        Assert.Throws<ArgumentNullException>(() => CSharpDelegateBuilder.Compile("   ", log, null, null, null));
    }

    [Fact]
    public async Task Build_ReturnsDelegateThatRunsScript_AndAppliesResponse()
    {
        var log = new Mock<ILogger>(MockBehavior.Loose).Object;
        // Ensure SharedStateStore has no problematic values (e.g., generic types) that break script prepends
        foreach (var key in SharedStateStore.KeySnapshot())
        {
            SharedStateStore.Set(key, null);
        }
        // Minimal script that compiles and runs without touching response
        var code = "int a = 1;";
        var del = CSharpDelegateBuilder.Build(code, log, args: null, extraImports: null, extraRefs: null);

        var http = new DefaultHttpContext();
        http.Request.Method = "GET";
        http.Request.Path = "/";

        await del(http);
        Assert.Equal(200, http.Response.StatusCode);
        Assert.Equal("text/plain; charset=utf-8", http.Response.ContentType);
    }
}
