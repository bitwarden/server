using Bit.Core.Enums;
using Bit.Core.Utilities;
using Xunit;

namespace Bit.Core.Test.Utilities;

public class EncryptedStringAttributeTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("aXY=|Y3Q=")] // Valid AesCbc256_B64
    [InlineData("aXY=|Y3Q=|cnNhQ3Q=")] // Valid AesCbc128_HmacSha256_B64
    [InlineData("Rsa2048_OaepSha256_B64.cnNhQ3Q=")]
    [InlineData("0.QmFzZTY0UGFydA==|QmFzZTY0UGFydA==")] // Valid AesCbc256_B64 as a number
    [InlineData("AesCbc256_B64.QmFzZTY0UGFydA==|QmFzZTY0UGFydA==")] // Valid AesCbc256_B64 as a number
    [InlineData("1.QmFzZTY0UGFydA==|QmFzZTY0UGFydA==|QmFzZTY0UGFydA==")] // Valid AesCbc128_HmacSha256_B64 as a number
    [InlineData("AesCbc128_HmacSha256_B64.QmFzZTY0UGFydA==|QmFzZTY0UGFydA==|QmFzZTY0UGFydA==")] // Valid AesCbc128_HmacSha256_B64 as a string
    [InlineData("2.QmFzZTY0UGFydA==|QmFzZTY0UGFydA==|QmFzZTY0UGFydA==")] // Valid AesCbc256_HmacSha256_B64 as a number
    [InlineData("AesCbc256_HmacSha256_B64.QmFzZTY0UGFydA==|QmFzZTY0UGFydA==|QmFzZTY0UGFydA==")] // Valid AesCbc256_HmacSha256_B64 as a string
    [InlineData("3.QmFzZTY0UGFydA==")] // Valid Rsa2048_OaepSha256_B64 as a number
    [InlineData("Rsa2048_OaepSha256_B64.QmFzZTY0UGFydA==")] // Valid Rsa2048_OaepSha256_B64 as a string
    [InlineData("4.QmFzZTY0UGFydA==")] // Valid Rsa2048_OaepSha1_B64 as a number
    [InlineData("Rsa2048_OaepSha1_B64.QmFzZTY0UGFydA==")] // Valid Rsa2048_OaepSha1_B64 as a string
    [InlineData("5.QmFzZTY0UGFydA==|QmFzZTY0UGFydA==")] // Valid Rsa2048_OaepSha256_HmacSha256_B64 as a number
    [InlineData("Rsa2048_OaepSha256_HmacSha256_B64.QmFzZTY0UGFydA==|QmFzZTY0UGFydA==")] // Valid Rsa2048_OaepSha256_HmacSha256_B64 as a string
    [InlineData("6.QmFzZTY0UGFydA==|QmFzZTY0UGFydA==")] // Valid Rsa2048_OaepSha1_HmacSha256_B64 as a number
    [InlineData("Rsa2048_OaepSha1_HmacSha256_B64.QmFzZTY0UGFydA==|QmFzZTY0UGFydA==")]
    public void IsValid_ReturnsTrue_WhenValid(string input)
    {
        var sut = new EncryptedStringAttribute();

        var actual = sut.IsValid(input);

        Assert.True(actual);
    }

    [Theory]
    [InlineData("")] // Empty string
    [InlineData(".")] // Split Character but two empty parts
    [InlineData("|")] // One encrypted part split character but empty parts
    [InlineData("||")] // Two encrypted part split character but empty parts
    [InlineData("!|!")] // Invalid base 64
    [InlineData("Rsa2048_OaepSha1_HmacSha256_B64.1")] // Invalid length
    [InlineData("Rsa2048_OaepSha1_HmacSha256_B64.|")] // Empty iv & ct
    [InlineData("AesCbc128_HmacSha256_B64.1")] // Invalid length
    [InlineData("AesCbc128_HmacSha256_B64.aXY=|Y3Q=|")] // Empty mac
    [InlineData("Rsa2048_OaepSha1_HmacSha256_B64.aXY=|Y3Q=|")] // Empty mac
    [InlineData("Rsa2048_OaepSha256_B64.1|2")] // Invalid length
    [InlineData("Rsa2048_OaepSha1_HmacSha256_B64.aXY=|")] // Empty mac
    [InlineData("254.QmFzZTY0UGFydA==")] // Bad Encryption type number
    [InlineData("0.QmFzZTY0UGFydA==|QmFzZTY0UGFydA==|QmFzZTY0UGFydA==")] // Invalid AesCbc256_B64 as a number
    [InlineData("AesCbc256_B64.QmFzZTY0UGFydA==|QmFzZTY0UGFydA==|QmFzZTY0UGFydA==")] // Invalid AesCbc256_B64 as a number
    [InlineData("1.QmFzZTY0UGFydA==|QmFzZTY0UGFydA==|QmFzZTY0UGFydA==|QmFzZTY0UGFydA==")] // Invalid AesCbc128_HmacSha256_B64 as a number
    [InlineData("AesCbc128_HmacSha256_B64.QmFzZTY0UGFydA==|QmFzZTY0UGFydA==|QmFzZTY0UGFydA==|QmFzZTY0UGFydA==")] // Invalid AesCbc128_HmacSha256_B64 as a string
    [InlineData("2.QmFzZTY0UGFydA==|QmFzZTY0UGFydA==|QmFzZTY0UGFydA==|QmFzZTY0UGFydA==")] // Invalid AesCbc256_HmacSha256_B64 as a number
    [InlineData("AesCbc256_HmacSha256_B64.QmFzZTY0UGFydA==|QmFzZTY0UGFydA==|QmFzZTY0UGFydA==|QmFzZTY0UGFydA==")] // Invalid AesCbc256_HmacSha256_B64 as a string
    [InlineData("3.QmFzZTY0UGFydA==|QmFzZTY0UGFydA==")] // Invalid Rsa2048_OaepSha256_B64 as a number
    [InlineData("Rsa2048_OaepSha256_B64.QmFzZTY0UGFydA==|QmFzZTY0UGFydA==")] // Invalid Rsa2048_OaepSha256_B64 as a string
    [InlineData("4.QmFzZTY0UGFydA==|QmFzZTY0UGFydA==")] // Invalid Rsa2048_OaepSha1_B64 as a number
    [InlineData("Rsa2048_OaepSha1_B64.QmFzZTY0UGFydA==|QmFzZTY0UGFydA==")] // Invalid Rsa2048_OaepSha1_B64 as a string
    [InlineData("5.QmFzZTY0UGFydA==|QmFzZTY0UGFydA==|QmFzZTY0UGFydA==")] // Invalid Rsa2048_OaepSha256_HmacSha256_B64 as a number
    [InlineData("Rsa2048_OaepSha256_HmacSha256_B64.QmFzZTY0UGFydA==|QmFzZTY0UGFydA==|QmFzZTY0UGFydA==")] // Invalid Rsa2048_OaepSha256_HmacSha256_B64 as a string
    [InlineData("6.QmFzZTY0UGFydA==|QmFzZTY0UGFydA==|QmFzZTY0UGFydA==")] // Invalid Rsa2048_OaepSha1_HmacSha256_B64 as a number
    [InlineData("Rsa2048_OaepSha1_HmacSha256_B64.QmFzZTY0UGFydA==")] // Invalid Rsa2048_OaepSha1_HmacSha256_B64 as a string
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
