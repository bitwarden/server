using Bit.Core.KeyManagement.Models.Data;
using Xunit;

namespace Bit.Core.Test.KeyManagement.Models.Data;

public class KeyIdTests
{
    private const string _keyIdA = "0123456789abcdef0123456789abcdef";

    [Theory]
    [InlineData("00000000000000000000000000000000")]
    [InlineData(_keyIdA)]
    [InlineData("ffffffffffffffffffffffffffffffff")]
    public void FromHexEncodedString_WithValidValue_RoundTrips(string hexEncodedKeyId)
    {
        Assert.Equal(hexEncodedKeyId, KeyId.FromHexEncodedString(hexEncodedKeyId).ToString());
    }

    [Fact]
    public void FromHexEncodedString_WithNull_ReturnsNull()
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
    public void FromHexEncodedString_WithInvalidValue_Throws(string hexEncodedKeyId)
    {
        Assert.Throws<ArgumentException>(() => KeyId.FromHexEncodedString(hexEncodedKeyId));
    }
}
