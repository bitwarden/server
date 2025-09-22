using System.Security.Cryptography;
using System.Text;
using Bit.Core.Utilities;
using Xunit;

namespace Bit.Core.Test.Utilities;

public class EnumerationProtectionHelpersTests
{
    #region GetIndexForInputHash Tests

    [Fact]
    public void GetIndexForInputHash_NullHmacKey_ReturnsZero()
    {
        // Arrange
        byte[] hmacKey = null;
        var salt = "test@example.com";
        var range = 10;

        // Act
        var result = EnumerationProtectionHelpers.GetIndexForInputHash(hmacKey, salt, range);

        // Assert
        Assert.Equal(0, result);
    }

    [Fact]
    public void GetIndexForInputHash_ZeroRange_ReturnsZero()
    {
        // Arrange
        var hmacKey = RandomNumberGenerator.GetBytes(32);
        var salt = "test@example.com";
        var range = 0;

        // Act
        var result = EnumerationProtectionHelpers.GetIndexForInputHash(hmacKey, salt, range);

        // Assert
        Assert.Equal(0, result);
    }

    [Fact]
    public void GetIndexForInputHash_NegativeRange_ReturnsZero()
    {
        // Arrange
        var hmacKey = RandomNumberGenerator.GetBytes(32);
        var salt = "test@example.com";
        var range = -5;

        // Act
        var result = EnumerationProtectionHelpers.GetIndexForInputHash(hmacKey, salt, range);

        // Assert
        Assert.Equal(0, result);
    }

    [Fact]
    public void GetIndexForInputHash_ValidInputs_ReturnsConsistentResult()
    {
        // Arrange
        var hmacKey = Encoding.UTF8.GetBytes("test-key-12345678901234567890123456789012");
        var salt = "test@example.com";
        var range = 10;

        // Act
        var result1 = EnumerationProtectionHelpers.GetIndexForInputHash(hmacKey, salt, range);
        var result2 = EnumerationProtectionHelpers.GetIndexForInputHash(hmacKey, salt, range);

        // Assert
        Assert.Equal(result1, result2);
        Assert.InRange(result1, 0, range - 1);
    }

    [Fact]
    public void GetIndexForInputHash_SameInputSameKey_AlwaysReturnsSameResult()
    {
        // Arrange
        var hmacKey = RandomNumberGenerator.GetBytes(32);
        var salt = "consistent@example.com";
        var range = 100;

        // Act - Call multiple times
        var results = new int[10];
        for (var i = 0; i < 10; i++)
        {
            results[i] = EnumerationProtectionHelpers.GetIndexForInputHash(hmacKey, salt, range);
        }

        // Assert - All results should be identical
        Assert.All(results, result => Assert.Equal(results[0], result));
        Assert.All(results, result => Assert.InRange(result, 0, range - 1));
    }

    [Fact]
    public void GetIndexForInputHash_DifferentInputsSameKey_ReturnsDifferentResults()
    {
        // Arrange
        var hmacKey = RandomNumberGenerator.GetBytes(32);
        var salt1 = "user1@example.com";
        var salt2 = "user2@example.com";
        var range = 100;

        // Act
        var result1 = EnumerationProtectionHelpers.GetIndexForInputHash(hmacKey, salt1, range);
        var result2 = EnumerationProtectionHelpers.GetIndexForInputHash(hmacKey, salt2, range);

        // Assert
        Assert.NotEqual(result1, result2);
        Assert.InRange(result1, 0, range - 1);
        Assert.InRange(result2, 0, range - 1);
    }

    [Fact]
    public void GetIndexForInputHash_DifferentKeysSameInput_ReturnsDifferentResults()
    {
        // Arrange
        var hmacKey1 = RandomNumberGenerator.GetBytes(32);
        var hmacKey2 = RandomNumberGenerator.GetBytes(32);
        var salt = "test@example.com";
        var range = 100;

        // Act
        var result1 = EnumerationProtectionHelpers.GetIndexForInputHash(hmacKey1, salt, range);
        var result2 = EnumerationProtectionHelpers.GetIndexForInputHash(hmacKey2, salt, range);

        // Assert
        Assert.NotEqual(result1, result2);
        Assert.InRange(result1, 0, range - 1);
        Assert.InRange(result2, 0, range - 1);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(100)]
    [InlineData(1000)]
    public void GetIndexForInputHash_VariousRanges_ReturnsValidIndex(int range)
    {
        // Arrange
        var hmacKey = RandomNumberGenerator.GetBytes(32);
        var salt = "test@example.com";

        // Act
        var result = EnumerationProtectionHelpers.GetIndexForInputHash(hmacKey, salt, range);

        // Assert
        Assert.InRange(result, 0, range - 1);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void GetIndexForInputHash_EmptyString_HandlesGracefully(string salt)
    {
        // Arrange
        var hmacKey = RandomNumberGenerator.GetBytes(32);

        // Act
        var result = EnumerationProtectionHelpers.GetIndexForInputHash(hmacKey, salt, 10);

        // Assert
        Assert.InRange(result, 0, 9);
    }

    [Fact]
    public void GetIndexForInputHash_NullInput_ThrowsException()
    {
        // Arrange
        var hmacKey = RandomNumberGenerator.GetBytes(32);
        string salt = null;
        var range = 10;

        // Act & Assert
        Assert.Throws<NullReferenceException>(() =>
            EnumerationProtectionHelpers.GetIndexForInputHash(hmacKey, salt, range));
    }

    [Fact]
    public void GetIndexForInputHash_SpecialCharacters_HandlesCorrectly()
    {
        // Arrange
        var hmacKey = RandomNumberGenerator.GetBytes(32);
        var salt = "test+user@example.com!@#$%^&*()";
        var range = 50;

        // Act
        var result1 = EnumerationProtectionHelpers.GetIndexForInputHash(hmacKey, salt, range);
        var result2 = EnumerationProtectionHelpers.GetIndexForInputHash(hmacKey, salt, range);

        // Assert
        Assert.Equal(result1, result2);
        Assert.InRange(result1, 0, range - 1);
    }

    [Fact]
    public void GetIndexForInputHash_UnicodeCharacters_HandlesCorrectly()
    {
        // Arrange
        var hmacKey = RandomNumberGenerator.GetBytes(32);
        var salt = "tëst@éxämplé.cöm";
        var range = 25;

        // Act
        var result1 = EnumerationProtectionHelpers.GetIndexForInputHash(hmacKey, salt, range);
        var result2 = EnumerationProtectionHelpers.GetIndexForInputHash(hmacKey, salt, range);

        // Assert
        Assert.Equal(result1, result2);
        Assert.InRange(result1, 0, range - 1);
    }

    [Fact]
    public void GetIndexForInputHash_LongInput_HandlesCorrectly()
    {
        // Arrange
        var hmacKey = RandomNumberGenerator.GetBytes(32);
        var salt = new string('a', 1000) + "@example.com";
        var range = 30;

        // Act
        var result = EnumerationProtectionHelpers.GetIndexForInputHash(hmacKey, salt, range);

        // Assert
        Assert.InRange(result, 0, range - 1);
    }

    #endregion
}
