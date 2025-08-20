using System;
using System.Linq;
using System.Security;
using Kestrun.Utilities;
using Xunit;

namespace KestrunTests.Utility;

public class SecureStringUtilsTests
{
    private static SecureString MakeSecure(string s)
    {
        var ss = new SecureString();
        foreach (var ch in s)
            ss.AppendChar(ch);
        ss.MakeReadOnly();
        return ss;
    }

    [Fact]
    public void ToSecureString_RoundTrip_Works()
    {
        var text = "s3cr3t!".AsSpan();
        var ss = text.ToSecureString();
        string? captured = null;
        ss.ToSecureSpan(span => captured = new string(span));
        Assert.Equal("s3cr3t!", captured);
    }

    [Fact]
    public void ToSecureSpan_ThrowsOnEmpty()
    {
        var empty = new SecureString();
        empty.MakeReadOnly();
        Assert.Throws<ArgumentException>(() => empty.ToSecureSpan(_ => { }));
    }

    [Fact]
    public void ToSecureString_ThrowsOnEmpty()
    {
        Assert.Throws<ArgumentException>(() => ReadOnlySpan<char>.Empty.ToSecureString());
    }
}
