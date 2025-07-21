using Kestrun;                         // ← contains static SharedState
using Xunit;

public class SharedStateTests
{
    // ── happy‑path basics ────────────────────────────────────────────
    [Fact]
    public void Set_And_TryGet_Work()
    {
        var host = new KestrunHost();
        host.SharedState.Remove("foo");

        Assert.True(host.SharedState.Set("foo", new List<int> { 1, 2 }));
        Assert.True(host.SharedState.TryGet("foo", out List<int>? list));
        Assert.Equal(2, list?.Count);
    }

    // ── case sensitivity ────────────────────────────────────────────
    [Fact]
    public void CaseInsensitive_Access_Works()
    {
        var host = new KestrunHost();
        host.SharedState.Set("Bar", "baz");

        Assert.True(host.SharedState.TryGet("bar", out string? val));
        Assert.Equal("baz", val);
    }

    // ── removal sanity ───────────────────────────────────────────────
    [Fact]
    public void Remove_Works()
    {
        var host = new KestrunHost();
        host.SharedState.Set("zap", new object());

        Assert.True(host.SharedState.Remove("ZAP"));       // note the casing
        Assert.False(host.SharedState.TryGet<object>("zap", out _));
    }

    // ── snapshot helpers ────────────────────────────────────────────
    [Fact]
    public void Snapshot_And_KeySnapshot_Work()
    {
        var host = new KestrunHost();
        host.SharedState.Set("snap", "val");

        var map = host.SharedState.Snapshot();
        var keys = host.SharedState.KeySnapshot();

        Assert.True(map.ContainsKey("snap"));
        Assert.Equal("val", map["snap"]);
        Assert.Contains("snap", keys, StringComparer.OrdinalIgnoreCase);
    }

    // ── defensive guards ────────────────────────────────────────────
    [Fact]
    public void Invalid_Name_Throws()
    {
        var host = new KestrunHost();
        Assert.Throws<ArgumentException>(() => host.SharedState.Set("1bad", "oops"));
        Assert.Throws<ArgumentException>(() => host.SharedState.Set("bad-name", "oops"));
    }

    [Fact]
    public void ValueType_Throws()
    {
        var host = new KestrunHost();
        Assert.Throws<ArgumentException>(() => host.SharedState.Set("num", 123)); // int ⇒ value‑type
    }
}
