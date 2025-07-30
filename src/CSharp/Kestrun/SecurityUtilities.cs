using System.Text;

namespace Kestrun;

public static class FixedTimeEquals
{
    /// <summary>
    /// Compares two byte arrays in constant time to prevent timing attacks.
    /// </summary>
    /// <param name="a">First byte array.</param>
    /// <param name="b">Second byte array.</param>
    /// <returns>True if both arrays are equal, false otherwise.</returns>  
    public static bool Test(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b)
    {
        if (a.Length != b.Length) return false;
        int diff = 0;
        for (int i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
        return diff == 0;
    }

    public static bool Test(string? a, string? b)
    {
        if (a == null || b == null)
            return false;

        var aBytes = Encoding.UTF8.GetBytes(a);
        var bBytes = Encoding.UTF8.GetBytes(b);
        return Test(aBytes, bBytes);
    }

    public static bool Test(ReadOnlySpan<byte> a, string? b)
    {
        if (b == null)
            return false;

        var bBytes = Encoding.UTF8.GetBytes(b);
        return Test(a, bBytes);
    }
    public static bool Test(string? a, ReadOnlySpan<byte> b)
    {
        if (a == null)
            return false;

        var aBytes = Encoding.UTF8.GetBytes(a);
        return Test(aBytes, b);
    }

    public static bool Test(byte[] a, string b) =>
    Test(a.AsSpan(), b);

    public static bool Test(byte[] a, byte[] b) =>
    Test(a.AsSpan(), b.AsSpan());

    public static bool Test(string? a, byte[] b) =>
    Test(a, b.AsSpan());
}