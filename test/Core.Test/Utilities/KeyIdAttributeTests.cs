using Bit.Core.Utilities;
using Xunit;

namespace Bit.Core.Test.Utilities;

public class UserKeyIdAttributeTests
{
    // The attribute defers to KeyId.FromHexEncodedString, so these cases pin the contract the
    // request models rely on rather than a second copy of the format rules.

    [Theory]
    [InlineData(null)] // Optional: Old users may miss a key-id
    [InlineData("00000000000000000000000000000000")]
    [InlineData("0123456789abcdef0123456789abcdef")]
    [InlineData("ffffffffffffffffffffffffffffffff")]
    public void IsValid_ReturnsTrue_WhenValid(string? input)
    {
        var sut = new KeyIdAttribute();

        Assert.True(sut.IsValid(input));
    }

    [Theory]
    [InlineData("")] // Empty
    [InlineData("0123456789abcdef0123456789abcde")] // 31 chars, one short
    [InlineData("0123456789abcdef0123456789abcdef0")] // 33 chars, one long
    [InlineData("0123456789ABCDEF0123456789abcdef")] // Uppercase hex
    [InlineData("0123456789abcdef0123456789abcdeg")] // 'g' is not hex
    [InlineData("0123456789abcdef 123456789abcdef")] // Whitespace
    [InlineData("0x23456789abcdef0123456789abcdef")] // Hex prefix inside the value
    public void IsValid_ReturnsFalse_WhenInvalid(string input)
    {
        var sut = new KeyIdAttribute();

        Assert.False(sut.IsValid(input));
    }

    [Fact]
    public void IsValid_ReturnsFalse_WhenNotAString()
    {
        var sut = new KeyIdAttribute();

        Assert.False(sut.IsValid(1234));
    }
}
