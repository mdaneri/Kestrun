using Kestrun;                         // ← contains static SharedState
using Xunit;

public class SharedStateTests
{
    // ── happy‑path basics ────────────────────────────────────────────
    [Fact]
    public void Set_And_TryGet_Work()
    {
        SharedState.Remove("foo");

        Assert.True(SharedState.Set("foo", new List<int> { 1, 2 }));
        Assert.True(SharedState.TryGet("foo", out List<int>? list));
        Assert.Equal(2, list?.Count);
    }

    // ── case sensitivity ────────────────────────────────────────────
    [Fact]
    public void CaseInsensitive_Access_Works()
    {
        SharedState.Set("Bar", "baz");

        Assert.True(SharedState.TryGet("bar", out string? val));
        Assert.Equal("baz", val);
    }

    // ── removal sanity ───────────────────────────────────────────────
    [Fact]
    public void Remove_Works()
    {
        SharedState.Set("zap", new object());

        Assert.True(SharedState.Remove("ZAP"));             // note the casing
        Assert.False(SharedState.TryGet<object>("zap", out _));
    }

    // ── snapshot helpers ────────────────────────────────────────────
    [Fact]
    public void Snapshot_And_KeySnapshot_Work()
    {
        SharedState.Set("snap", "val");

        var map  = SharedState.Snapshot();
        var keys = SharedState.KeySnapshot();

        Assert.True(map.ContainsKey("snap"));
        Assert.Equal("val", map["snap"]);
        Assert.Contains("snap", keys, StringComparer.OrdinalIgnoreCase);
    }

    // ── defensive guards ────────────────────────────────────────────
    [Fact]
    public void Invalid_Name_Throws()
    {
        Assert.Throws<ArgumentException>(() => SharedState.Set("1bad",  "oops"));
        Assert.Throws<ArgumentException>(() => SharedState.Set("bad-name", "oops"));
    }

    [Fact]
    public void ValueType_Throws()
    {
        Assert.Throws<ArgumentException>(() => SharedState.Set("num", 123)); // int ⇒ value‑type
    }
}
