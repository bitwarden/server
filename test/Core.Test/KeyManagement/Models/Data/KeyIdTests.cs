using Bit.Core.KeyManagement.Models.Data;
using Xunit;

namespace Bit.Core.Test.KeyManagement.Models.Data;

public class KeyIdTests
{
    private const string _keyIdA = "0123456789abcdef0123456789abcdef";
    private const string _keyIdB = "fedcba9876543210fedcba9876543210";

    [Theory]
    [InlineData("00000000000000000000000000000000")]
    [InlineData(_keyIdA)]
    [InlineData("ffffffffffffffffffffffffffffffff")]
    public void TryFromHexEncodedString_WithValidValue_RoundTrips(string hexEncodedKeyId)
    {
        Assert.Equal(hexEncodedKeyId, KeyId.FromHexEncodedString(hexEncodedKeyId).ToString());
    }

    [Fact]
    public void TryFromHexEncodedString_WithNull_ReturnsNull()
    {
        Assert.Null(KeyId.FromHexEncodedString(null));
    }

    [Theory]
    [InlineData("")] // Empty
    [InlineData("0123456789abcdef0123456789abcde")] // 31 characters, one short
    [InlineData("0123456789abcdef0123456789abcdef0")] // 33 characters, one long
    [InlineData("0123456789ABCDEF0123456789abcdef")] // Uppercase hex
    [InlineData("0123456789abcdef0123456789abcdeg")] // 'g' is not hex
    [InlineData("0123456789abcdef 123456789abcdef")] // Whitespace
    public void TryFromHexEncodedString_WithInvalidValue_Throws(string hexEncodedKeyId)
    {
        Assert.Throws<ArgumentException>(() => KeyId.FromHexEncodedString(hexEncodedKeyId));
    }

    // Two key ids built from the same string are distinct instances, so these comparisons only hold
    // because KeyId compares by value. Callers rely on that for == / != and for dictionary lookups.
    [Fact]
    public void ComparesByValue()
    {
        var a1 = KeyId.FromHexEncodedString(_keyIdA);
        var a2 = KeyId.FromHexEncodedString(_keyIdA);
        var b = KeyId.FromHexEncodedString(_keyIdB);

        Assert.Equal(a1, a2);
        Assert.True(a1 == a2);
        Assert.False(a1 != a2);
        Assert.Equal(a1.GetHashCode(), a2.GetHashCode());

        Assert.NotEqual(a1, b);
        Assert.False(a1 == b);
        Assert.True(a1 != b);
    }

    [Fact]
    public void ComparesByValue_WithNulls()
    {
        var a = KeyId.FromHexEncodedString(_keyIdA);
        KeyId? nothing = null;

        Assert.False(a == nothing);
        Assert.True(a != nothing);
        Assert.False(nothing == a);
        Assert.True(nothing != a);
        Assert.True(nothing == null);
    }
}
