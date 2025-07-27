using Kestrun;
using Kestrun.Utilities;
using System.Collections;
using System.Management.Automation;
using Xunit;

#pragma warning disable CA1050 // Declare types in namespaces
public class YamlHelperTests
#pragma warning restore CA1050 // Declare types in namespaces
{
    [Fact]
    public void ToYaml_SerializesObject()
    {
        var ht = new Hashtable { { "name", "foo" }, { "value", 1 } };
        var yaml = YamlHelper.ToYaml(ht);
        Assert.Contains("name: foo", yaml);
        Assert.Contains("value: 1", yaml);
    }

    [Fact]
    public void FromYamlToHashtable_RoundTrip()
    {
        string yaml = "name: foo\nvalue: 1";
        var ht = YamlHelper.FromYamlToHashtable(yaml);
        Assert.Equal("foo", ht["name"]);
        Assert.NotNull(ht["value"]);
        Assert.Equal(1, Convert.ToInt32(ht["value"]));
    }

    [Fact]
    public void FromYamlToPSCustomObject_RoundTrip()
    {
        string yaml = "name: foo\nvalue: 1";
        var obj = YamlHelper.FromYamlToPSCustomObject(yaml);
        Assert.Equal("foo", obj.Members["name"].Value);
        Assert.Equal("1", obj.Members["value"].Value);
    }
}
