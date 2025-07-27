using Kestrun;
using Xunit;

#pragma warning disable CA1050 // Declare types in namespaces
public class KestrunHostScriptTests
#pragma warning restore CA1050 // Declare types in namespaces
{
    [Fact]
    public void IsCSharpScriptValid_ReturnsTrueForValid()
    {
        var host = new KestrunHost("TestHost");
        bool valid = host.IsCSharpScriptValid("System.Console.WriteLine(\"hi\");");
        Assert.True(valid);
    }

    [Fact]
    public void IsCSharpScriptValid_ReturnsFalseForInvalid()
    {
        var host = new KestrunHost("TestHost");
        bool valid = host.IsCSharpScriptValid("System.Console.Writeline(\"hi\");");
        Assert.False(valid);
    }

    [Fact]
    public void GetCSharpScriptErrors_ReturnsMessage()
    {
        var host = new KestrunHost("TestHost");
        var msg = host.GetCSharpScriptErrors("System.Console.Writeline(\"hi\");");
        Assert.NotNull(msg);
    }
}
