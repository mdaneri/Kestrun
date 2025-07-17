using KestrumLib;
using System.Collections.Generic;
using Xunit;

public class GlobalVariablesTests
{
    [Fact]
    public void Define_And_TryGet_Works()
    {
        GlobalVariables.Remove("foo");
        GlobalVariables.Define("foo", new List<int> { 1,2 });
        Assert.True(GlobalVariables.TryGet("foo", out List<int>? val));
        Assert.Equal(2, val?.Count);
    }

    [Fact]
    public void Remove_RespectsReadOnly()
    {
        GlobalVariables.Define("ro", new object(), readOnly: true);
        Assert.False(GlobalVariables.Remove("ro"));
    }

    [Fact]
    public void UpdateValue_Works()
    {
        GlobalVariables.Define("upd", new List<int>());
        GlobalVariables.UpdateValue("upd", new List<int>{1});
        var obj = GlobalVariables.Get("upd") as List<int>;
        Assert.Single(obj!);
    }
}
