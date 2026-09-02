using Bit.Core.Enums;
using Bit.Core.KeyManagement.Utilities;
using Bit.Test.Common.Constants;
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
    [InlineData(TestEncryptionConstants.AES256_CBC_B64_Encstring, EncryptionType.AesCbc256_B64)]
    [InlineData(TestEncryptionConstants.AES256_CBC_HMAC_Encstring, EncryptionType.AesCbc256_HmacSha256_B64)]
    [InlineData(TestEncryptionConstants.RSA2048_OAEPSHA1_B64_Encstring, EncryptionType.Rsa2048_OaepSha1_B64)]
    [InlineData(TestEncryptionConstants.V2PrivateKey, EncryptionType.XChaCha20Poly1305_B64)]
    [InlineData(TestEncryptionConstants.V2WrappedSigningKey, EncryptionType.XChaCha20Poly1305_B64)]
    [InlineData(TestEncryptionConstants.AES256_CBC_HMAC_EmptySuffix, EncryptionType.AesCbc256_HmacSha256_B64)] // empty suffix still valid
    [InlineData(TestEncryptionConstants.XCHACHA20POLY1305_B64_Encstring, EncryptionType.XChaCha20Poly1305_B64)]
    public void GetEncryptionType_WithValidString_ReturnsExpected(string input, EncryptionType expected)
    {
        var result = EncryptionParsing.GetEncryptionType(input);
        Assert.Equal(expected, result);
    }
}

