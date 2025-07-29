using Kestrun;
using Kestrun.Utilities;
using System.Collections;
using System.Collections.Generic;
using System.Xml.Linq;
using Xunit;

namespace KestrunTests.Utility;
public class XmlUtilTests

{
    [Fact]
    public void ToXml_Null_ReturnsNilElement()
    {
        var elem = XmlUtil.ToXml("Value", null);
        Assert.Equal("Value", elem.Name);
        Assert.Equal("true", elem.Attribute(XName.Get("nil", "http://www.w3.org/2001/XMLSchema-instance"))?.Value);
    }

    [Fact]
    public void ToXml_Primitive_ReturnsElementWithValue()
    {
        var elem = XmlUtil.ToXml("Number", 42);
        Assert.Equal("42", elem.Value);
    }

    [Fact]
    public void ToXml_Dictionary_ReturnsNestedElements()
    {
        var dict = new Hashtable { { "A", 1 }, { "B", 2 } };
        var elem = XmlUtil.ToXml("Dict", dict);
        Assert.Equal("1", elem.Element("A")?.Value);
        Assert.Equal("2", elem.Element("B")?.Value);
    }

    [Fact]
    public void ToXml_List_ReturnsItemElements()
    {
        var list = new List<int> { 1, 2 };
        var elem = XmlUtil.ToXml("List", list);
        Assert.Collection(elem.Elements(), e => Assert.Equal("1", e.Value), e => Assert.Equal("2", e.Value));
    }

    private class Sample
    {
        public string Name { get; set; } = "Foo";
    }

    [Fact]
    public void ToXml_Object_UsesProperties()
    {
        var sample = new Sample();
        var elem = XmlUtil.ToXml("Sample", sample);
        Assert.Equal("Foo", elem.Element("Name")?.Value);
    }
}
