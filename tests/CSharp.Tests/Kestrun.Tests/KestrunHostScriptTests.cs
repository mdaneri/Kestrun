using Kestrun;
using Xunit;

public class KestrunHostScriptTests
{
    [Fact]
    public void IsCSharpScriptValid_ReturnsTrueForValid()
    {
        var host = new KestrunHost();
        bool valid = host.IsCSharpScriptValid("System.Console.WriteLine(\"hi\");");
        Assert.True(valid);
    }

    [Fact]
    public void IsCSharpScriptValid_ReturnsFalseForInvalid()
    {
        var host = new KestrunHost();
        bool valid = host.IsCSharpScriptValid("System.Console.Writeline(\"hi\");");
        Assert.False(valid);
    }

    [Fact]
    public void GetCSharpScriptErrors_ReturnsMessage()
    {
        var host = new KestrunHost();
        var msg = host.GetCSharpScriptErrors("System.Console.Writeline(\"hi\");");
        Assert.NotNull(msg);
    }
}
