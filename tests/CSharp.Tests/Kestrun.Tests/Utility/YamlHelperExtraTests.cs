using System.Collections;
using Kestrun.Utilities;
using Xunit;

namespace KestrunTests.Utility;

public class YamlHelperExtraTests
{
    [Fact]
    [Trait("Category", "Utility")]
    public void ToYaml_SerializesHashtableAndList()
    {
        var ht = new Hashtable
        {
            ["a"] = 1,
            ["b"] = new ArrayList { "x", 2 }
        };
        var yaml = YamlHelper.ToYaml(ht);
        Assert.Contains("a:", yaml);
        Assert.Contains("b:", yaml);
    }

    [Fact]
    [Trait("Category", "Utility")]
    public void FromYaml_Deserializes_ToHashtable_And_PSCustomObject()
    {
        var yaml = "a: 1\nb:\n - x\n - 2\n";
        var ht = YamlHelper.FromYamlToHashtable(yaml);
        Assert.Equal(1, Convert.ToInt32(ht["a"]));

        var obj = YamlHelper.FromYamlToPSCustomObject(yaml);
        var aval = obj.Properties["a"].Value;
        Assert.Equal(1, Convert.ToInt32(aval));
    }
}
