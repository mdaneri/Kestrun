using Kestrun.Utilities;
using Kestrun.Hosting;
using Kestrun.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Xunit;

namespace KestrunTests.Utility;

public class VariablesMapTests
{
    private static (KestrunContext Ctx, DefaultHttpContext Http) MakeContext()
    {
        var http = new DefaultHttpContext();
        http.Request.Method = "GET";
        http.Request.Path = "/hello";
        http.Features.Set<ISessionFeature>(new SessionFeature { Session = new DummySession() });
        http.Request.Headers["User-Agent"] = "xunit";

        var req = new KestrunRequest
        {
            Method = http.Request.Method,
            Path = http.Request.Path,
            Query = [],
            Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["User-Agent"] = "xunit"
            },
            Body = string.Empty
        };
        var resp = new KestrunResponse(req);
        var ctx = new KestrunContext(req, resp, http);
        return (ctx, http);
    }

    [Fact]
    public void GetCommonProperties_PopulatesExpectedKeys()
    {
        var (ctx, _) = MakeContext();
        Dictionary<string, object?> vars = new(StringComparer.OrdinalIgnoreCase);
        var ok = VariablesMap.GetCommonProperties(ctx, ref vars);
        Assert.True(ok);
        var required = new[] { "Context", "Request", "Headers", "ServerName", "Timestamp", "UserAgent" };
        foreach (var k in required)
        {
            Assert.Contains(k, vars.Keys);
        }
    }

    [Fact]
    public void GetVariablesMap_AddsSharedState_And_Common()
    {
        var (ctx, _) = MakeContext();
        Dictionary<string, object?> vars = null!;
        var ok = VariablesMap.GetVariablesMap(ctx, ref vars);
        Assert.True(ok);
        Assert.NotNull(vars);
        Assert.Contains("Context", vars.Keys);
        Assert.Contains("Request", vars.Keys);
    }

    private sealed class SessionFeature : ISessionFeature
    {
        public ISession Session { get; set; } = default!;
    }

    private sealed class DummySession : ISession
    {
        public bool IsAvailable => true;
        public string Id => "dummy";
        public IEnumerable<string> Keys => [];
        public void Clear() { }
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Remove(string key) { }
        public void Set(string key, byte[] value) { }
        public bool TryGetValue(string key, out byte[] value)
        {
            value = [];
            return false;
        }
    }
}
