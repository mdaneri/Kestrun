using System;
using System.Linq;
using Kestrun.Jwt;
using Xunit;

namespace KestrunTests.Jwt;

public class JwtInspectorTests
{
    [Fact]
    public void ReadAllParameters_ExtractsHeaderPayloadAndClaims()
    {
        // Create a simple HS256 token
        var secretB64u = B64Url(Enumerable.Repeat((byte)0xEF, 32).ToArray());
        var builder = JwtTokenBuilder
            .New()
            .WithIssuer("iss-x")
            .WithAudience("aud-y")
            .WithSubject("sub-z")
            .AddClaim("scope", "read:all")
            .ValidFor(TimeSpan.FromMinutes(1))
            .SignWithSecret(secretB64u);

        var token = builder.Build().Token();

        var p = JwtInspector.ReadAllParameters(token);

        Assert.Equal("iss-x", p.Issuer);
        Assert.Contains("aud-y", p.Audiences);
        Assert.Equal("sub-z", p.Subject);
        Assert.NotNull(p.NotBefore);
        Assert.NotNull(p.Expires);
        Assert.NotNull(p.IssuedAt);
        Assert.Equal("JWT", p.Type); // typ
        Assert.False(string.IsNullOrWhiteSpace(p.Algorithm));
        Assert.True(p.Header.Count >= 1);
        Assert.Equal("read:all", p.Claims["scope"]);
    }

    private static string B64Url(byte[] bytes)
    {
        var s = Convert.ToBase64String(bytes);
        s = s.Replace('+', '-').Replace('/', '_');
        return s.TrimEnd('=');
    }
}
