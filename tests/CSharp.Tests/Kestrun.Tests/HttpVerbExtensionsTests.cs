using Kestrun;
using Kestrun.Utilities;
using Xunit;

#pragma warning disable CA1050 // Declare types in namespaces
public class HttpVerbExtensionsTests
#pragma warning restore CA1050 // Declare types in namespaces
{
    [Theory]
    [InlineData(HttpVerb.Get, "GET")]
    [InlineData(HttpVerb.Post, "POST")]
    [InlineData(HttpVerb.Delete, "DELETE")]
    [InlineData(HttpVerb.Head, "HEAD")]
    [InlineData(HttpVerb.Put, "PUT")]
    [InlineData(HttpVerb.Patch, "PATCH")]
    [InlineData(HttpVerb.Options, "OPTIONS")]
    [InlineData(HttpVerb.Trace, "TRACE")]
    public void ToMethodString_ReturnsUpperCase(HttpVerb verb, string expected)
    {
        Assert.Equal(expected, verb.ToMethodString());
    }
}
