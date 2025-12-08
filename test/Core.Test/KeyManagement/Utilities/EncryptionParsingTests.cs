using Bit.Core.Enums;
using Bit.Core.KeyManagement.Utilities;
using Xunit;

namespace Bit.Core.Test.KeyManagement.Utilities;

public class EncryptionParsingTests
{
    [Fact]
    public void GetEncryptionType_WithNull_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => EncryptionParsing.GetEncryptionType(null));
    }

    [Theory]
    [InlineData("2")] // missing '.' separator
    [InlineData("abc.def")] // non-numeric prefix
    [InlineData("8.any")] // undefined enum value
    [InlineData("255.any")] // out of defined enum range
    public void GetEncryptionType_WithInvalidString_ThrowsArgumentException(string input)
    {
        Assert.Throws<ArgumentException>(() => EncryptionParsing.GetEncryptionType(input));
    }

    [Theory]
    [InlineData("0.foo", EncryptionType.AesCbc256_B64)]
    [InlineData("1.bar", EncryptionType.AesCbc128_HmacSha256_B64)]
    [InlineData("2.qux", EncryptionType.AesCbc256_HmacSha256_B64)]
    [InlineData("3.any", EncryptionType.Rsa2048_OaepSha256_B64)]
    [InlineData("4.any", EncryptionType.Rsa2048_OaepSha1_B64)]
    [InlineData("5.any", EncryptionType.Rsa2048_OaepSha256_HmacSha256_B64)]
    [InlineData("6.any", EncryptionType.Rsa2048_OaepSha1_HmacSha256_B64)]
    [InlineData("7.any", EncryptionType.XChaCha20Poly1305_B64)]
    [InlineData("2.", EncryptionType.AesCbc256_HmacSha256_B64)] // empty suffix still valid
    public void GetEncryptionType_WithValidString_ReturnsExpected(string input, EncryptionType expected)
    {
        var result = EncryptionParsing.GetEncryptionType(input);
        Assert.Equal(expected, result);
    }
}

