using Kestrun;                         // ← contains static SharedState
using Kestrun.SharedState;
using Xunit;

namespace KestrunTests.SharedState;

public class SharedStateTests

{
    // ── happy‑path basics ────────────────────────────────────────────
    [Fact]
    public void Set_And_TryGet_Work()
    {
        var host = new KestrunHost("TestHost", AppContext.BaseDirectory);

        Assert.True(SharedStateStore.Set("foo", new List<int> { 1, 2 }));
        Assert.True(SharedStateStore.TryGet("foo", out List<int>? list));
        Assert.Equal(2, list?.Count);
    }

    // ── case sensitivity ────────────────────────────────────────────
    [Fact]
    public void CaseInsensitive_Access_Works()
    {
        var host = new KestrunHost("TestHost", AppContext.BaseDirectory);
        SharedStateStore.Set("Bar", "baz");

        Assert.True(SharedStateStore.TryGet("bar", out string? val));
        Assert.Equal("baz", val);
    }



    // ── snapshot helpers ────────────────────────────────────────────
    [Fact]
    public void Snapshot_And_KeySnapshot_Work()
    {
        var host = new KestrunHost("TestHost", AppContext.BaseDirectory);
        SharedStateStore.Set("snap", "val");

        var map = SharedStateStore.Snapshot();
        var keys = SharedStateStore.KeySnapshot();

        Assert.True(map.ContainsKey("snap"));
        Assert.Equal("val", map["snap"]);
        Assert.Contains("snap", keys, StringComparer.OrdinalIgnoreCase);
    }

    // ── defensive guards ────────────────────────────────────────────
    [Fact]
    public void Invalid_Name_Throws()
    {
        var host = new KestrunHost("TestHost", AppContext.BaseDirectory);
        Assert.Throws<ArgumentException>(() => SharedStateStore.Set("1bad", "oops"));
        Assert.Throws<ArgumentException>(() => SharedStateStore.Set("bad-name", "oops"));
    }

    [Fact]
    public void ValueType_Throws()
    {
        var host = new KestrunHost("TestHost", AppContext.BaseDirectory);
        Assert.Throws<ArgumentException>(() => SharedStateStore.Set("num", 123)); // int ⇒ value‑type
    }
}
