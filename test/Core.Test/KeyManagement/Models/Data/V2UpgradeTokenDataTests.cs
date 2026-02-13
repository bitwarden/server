using Bit.Core.KeyManagement.Models.Data;
using Xunit;

namespace Bit.Core.Test.KeyManagement.Models.Data;

public class V2UpgradeTokenDataTests
{
    [Fact]
    public void ToJson_SerializesCorrectly()
    {
        var data = new V2UpgradeTokenData
        {
            WrappedUserKey1 = "2.key1==|data1==|hmac1==",
            WrappedUserKey2 = "2.key2==|data2==|hmac2=="
        };

        var json = data.ToJson();

        var expected = """{"WrappedUserKey1":"2.key1==|data1==|hmac1==","WrappedUserKey2":"2.key2==|data2==|hmac2=="}""";
        Assert.Equal(expected, json);
    }

    [Fact]
    public void FromJson_ValidJson_DeserializesCorrectly()
    {
        var json = """{"WrappedUserKey1":"2.key1==|data1==|hmac1==","WrappedUserKey2":"2.key2==|data2==|hmac2=="}""";

        var result = V2UpgradeTokenData.FromJson(json);

        Assert.NotNull(result);
        Assert.Equal("2.key1==|data1==|hmac1==", result.WrappedUserKey1);
        Assert.Equal("2.key2==|data2==|hmac2==", result.WrappedUserKey2);
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
