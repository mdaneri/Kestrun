using Kestrun.Utilities;
using Xunit;

#pragma warning disable CA1050 // Declare types in namespaces
public class SecurityUtilitiesTests
#pragma warning restore CA1050 // Declare types in namespaces
{
    [Fact]
    public void FixedTimeEquals_ByteArrays_Equal_ReturnsTrue()
    {
        byte[] a = { 1, 2, 3 };
        byte[] b = { 1, 2, 3 };
        Assert.True(SecurityUtilities.FixedTimeEquals(a, b));
    }

    [Fact]
    public void FixedTimeEquals_ByteArrays_Different_ReturnsFalse()
    {
        byte[] a = { 1, 2, 3 };
        byte[] b = { 1, 2, 4 };
        Assert.False(SecurityUtilities.FixedTimeEquals(a, b));
    }

    [Fact]
    public void FixedTimeEquals_Strings_Equal_ReturnsTrue()
    {
        Assert.True(SecurityUtilities.FixedTimeEquals("abc", "abc"));
    }

    [Fact]
    public void FixedTimeEquals_Strings_Null_ReturnsFalse()
    {
        Assert.False(SecurityUtilities.FixedTimeEquals(null, "abc"));
        Assert.False(SecurityUtilities.FixedTimeEquals("abc", null));
    }
}
