using Kestrun;
using Kestrun.Utilities;
using Xunit;

public class HttpVerbExtensionsTests
{
    [Theory]
    [InlineData(HttpVerb.Get, "GET")]
    [InlineData(HttpVerb.Post, "POST")]
    [InlineData(HttpVerb.Delete, "DELETE")]
    public void ToMethodString_ReturnsUpperCase(HttpVerb verb, string expected)
    {
        Assert.Equal(expected, verb.ToMethodString());
    }
}
