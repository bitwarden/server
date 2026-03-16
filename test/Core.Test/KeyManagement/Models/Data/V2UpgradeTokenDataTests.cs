using Bit.Core.KeyManagement.Models.Data;
using Xunit;

namespace Bit.Core.Test.KeyManagement.Models.Data;

public class V2UpgradeTokenDataTests
{
    private static readonly string _mockEncryptedType2String =
        "2.AOs41Hd8OQiCPXjyJKCiDA==|O6OHgt2U2hJGBSNGnimJmg==|iD33s8B69C8JhYYhSa4V1tArjvLr8eEaGqOV7BRo5Jk=";
    private static readonly string _mockEncryptedType7String = "7.AOs41Hd8OQiCPXjyJKCiDA==";

    [Fact]
    public void ToJson_SerializesCorrectly()
    {
        var data = new V2UpgradeTokenData
        {
            WrappedUserKey1 = _mockEncryptedType7String,
            WrappedUserKey2 = _mockEncryptedType2String
        };

        var json = data.ToJson();

        var expected = $"{{\"WrappedUserKey1\":\"{_mockEncryptedType7String}\",\"WrappedUserKey2\":\"{_mockEncryptedType2String}\"}}";
        Assert.Equal(expected, json);
    }

    [Fact]
    public void FromJson_ValidJson_DeserializesCorrectly()
    {
        var json = $"{{\"WrappedUserKey1\":\"{_mockEncryptedType7String}\",\"WrappedUserKey2\":\"{_mockEncryptedType2String}\"}}";

        var result = V2UpgradeTokenData.FromJson(json);

        Assert.NotNull(result);
        Assert.Equal(_mockEncryptedType7String, result.WrappedUserKey1);
        Assert.Equal(_mockEncryptedType2String, result.WrappedUserKey2);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void FromJson_NullOrEmptyInput_ReturnsNull(string? input)
    {
        var result = V2UpgradeTokenData.FromJson(input);

        Assert.Null(result);
    }

    [Fact]
    public void FromJson_InvalidJson_ReturnsNull()
    {
        var result = V2UpgradeTokenData.FromJson("{\"invalid\": json}");

        Assert.Null(result);
    }
}
