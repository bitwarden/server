using Bit.Core.Enums;
using Bit.Core.Utilities;
using Xunit;

namespace Bit.Core.Test.Utilities;

public class EncryptedStringAttributeTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("AAECAwQFBgcICQoLDA0ODw==|Y3Q=")] // Valid AesCbc256_B64
    [InlineData("AAECAwQFBgcICQoLDA0ODw==|Y3Q=|AAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8=")] // Valid legacy headerless iv|ct|mac
    [InlineData("0.AAECAwQFBgcICQoLDA0ODw==|QmFzZTY0UGFydA==")] // Valid AesCbc256_B64 as a number
    [InlineData("2.AAECAwQFBgcICQoLDA0ODw==|QmFzZTY0UGFydA==|AAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8=")] // Valid AesCbc256_HmacSha256_B64 as a number
    [InlineData("3.QmFzZTY0UGFydA==")] // Valid Rsa2048_OaepSha256_B64 as a number
    [InlineData("4.QmFzZTY0UGFydA==")] // Valid Rsa2048_OaepSha1_B64 as a number
    [InlineData("5.QmFzZTY0UGFydA==|QmFzZTY0UGFydA==")] // Valid Rsa2048_OaepSha256_HmacSha256_B64 as a number
    [InlineData("6.QmFzZTY0UGFydA==|QmFzZTY0UGFydA==")] // Valid Rsa2048_OaepSha1_HmacSha256_B64 as a number
    [InlineData("7.QmFzZTY0UGFydA==")] // Valid CoseEncrypt0B64 as a number
    [InlineData("0.AAECAwQFBgcICQoLDA0ODw==|AAAA")] // Unpadded CT with padded IV
    [InlineData("AAECAwQFBgcICQoLDA0OD/==|lGD=")] // Non-canonical = padding on both pieces (headerless)
    [InlineData("2.AAECAwQFBgcICQoLDA0OD/==|lGD=|AAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh/=")] // Non-canonical padding on all three pieces
    [InlineData("0.AAECAwQFBgcICQoLDA0ODw==|QmFzZTY0UGFydB==")] // Non-canonical == in longer piece (exercises prefix validation)
    public void IsValid_ReturnsTrue_WhenValid(string? input)
    {
        var sut = new EncryptedStringAttribute();

        var actual = sut.IsValid(input);

        Assert.True(actual);
    }

    [Theory]
    [InlineData("Test")] // Plain text injection attack - DoS vulnerability regression test
    [InlineData("Hello World")] // Plain text injection attack
    [InlineData("SecretPassword123")] // Plain text injection attack
    [InlineData("")] // Empty string
    [InlineData(".")] // Split Character but two empty parts
    [InlineData("|")] // One encrypted part split character but empty parts
    [InlineData("||")] // Two encrypted part split character but empty parts
    [InlineData("!|!")] // Invalid base 64
    [InlineData("254.QmFzZTY0UGFydA==")] // Bad Encryption type number
    [InlineData("0.QmFzZTY0UGFydA==|QmFzZTY0UGFydA==|QmFzZTY0UGFydA==")] // Invalid AesCbc256_B64 as a number
    [InlineData("1.AAECAwQFBgcICQoLDA0ODw==|QmFzZTY0UGFydA==|AAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8=")] // Removed encryption type number
    [InlineData("2.QmFzZTY0UGFydA==|QmFzZTY0UGFydA==|QmFzZTY0UGFydA==|QmFzZTY0UGFydA==")] // Invalid AesCbc256_HmacSha256_B64 as a number
    [InlineData("3.QmFzZTY0UGFydA==|QmFzZTY0UGFydA==")] // Invalid Rsa2048_OaepSha256_B64 as a number
    [InlineData("4.QmFzZTY0UGFydA==|QmFzZTY0UGFydA==")] // Invalid Rsa2048_OaepSha1_B64 as a number
    [InlineData("5.QmFzZTY0UGFydA==|QmFzZTY0UGFydA==|QmFzZTY0UGFydA==")] // Invalid Rsa2048_OaepSha256_HmacSha256_B64 as a number
    [InlineData("6.QmFzZTY0UGFydA==|QmFzZTY0UGFydA==|QmFzZTY0UGFydA==")] // Invalid Rsa2048_OaepSha1_HmacSha256_B64 as a number
    [InlineData("0.AA!!AB==|Y3Q=")] // Invalid char in prefix with non-canonical last char
    [InlineData("0.AAAAB==|Y3Q=")] // Piece length not multiple of 4
    [InlineData("0.====|Y3Q=")] // Padding-only piece
    [InlineData("2.AAECAw==|Y3Q=|AAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8=")] // IV decodes to 4 bytes instead of 16
    [InlineData("0.AAECAw==|Y3Q=")] // IV decodes to 4 bytes instead of 16
    [InlineData("AAECAw==|Y3Q=")] // Short IV, headerless AesCbc256_B64
    [InlineData("0.AAAA|Y3Q=")] // IV decodes to 3 bytes instead of 16
    [InlineData("0.AAECAwQFBgcICQoLDA0ODxA=|Y3Q=")] // IV decodes to 17 bytes, length is an equality check
    [InlineData("2.AAECAwQFBgcICQoLDA0ODw==|Y3Q=|AAECAw==")] // Mac decodes to 4 bytes instead of 32
    [InlineData("AAECAwQFBgcICQoLDA0ODw==|Y3Q=|AAECAw==")] // Short mac, headerless iv|ct|mac
    [InlineData("2.lGD=|lGD=|lGD=")] // Non-canonical padding, but short IV and mac
    public void IsValid_ReturnsFalse_WhenInvalid(string input)
    {
        var sut = new EncryptedStringAttribute();

        var actual = sut.IsValid(input);

        Assert.False(actual);
    }

    [Fact]
    public void EncryptionTypeMap_HasEntry_ForEachEnumValue()
    {
        var enumValues = Enum.GetValues<EncryptionType>();
        Assert.Equal(enumValues.Length, EncryptedStringAttribute._encryptionTypeToRequiredPiecesMap.Count);

        foreach (var enumValue in enumValues)
        {
            // Go a step further and ensure that the map contains a value for each value instead of just casting
            // a random number for one of the keys.
            Assert.True(EncryptedStringAttribute._encryptionTypeToRequiredPiecesMap.ContainsKey(enumValue));
        }
    }

    [Fact]
    public void CheckForUnderlyingTypeChange()
    {
        var underlyingType = typeof(EncryptionType).GetEnumUnderlyingType();
        var expectedType = typeof(byte);

        Assert.True(underlyingType == expectedType,
            $"Hello future person, it seems you have changed the underlying type for {nameof(EncryptionType)}, " +
            $"that is totally fine you just also need to change the line for {expectedType.Name}.TryParse in " +
            $"{nameof(EncryptedStringAttribute)} to {underlyingType.Name}.TryParse (but you can probably use the alias)" +
            "and then update this test!");
    }
}
