using Bit.Api.Utilities;
using Xunit;

namespace Bit.Api.Test.Utilities;

public class ValidJsonArrayAttributeTests
{
    private readonly ValidJsonArrayAttribute _sut = new();

    [Theory]
    [InlineData("[]")]
    [InlineData("[{\"field\":\"value\"}]")]
    [InlineData("[1, 2, 3]")]
    public void IsValid_WithValidJsonArray_ReturnsTrue(string input)
    {
        Assert.True(_sut.IsValid(input));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("\"string\"")]
    [InlineData("not json")]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("")]
    public void IsValid_WithNonArrayOrInvalidJson_ReturnsFalse(string input)
    {
        Assert.False(_sut.IsValid(input));
    }

    [Fact]
    public void IsValid_WithNull_ReturnsFalse()
    {
        Assert.False(_sut.IsValid(null));
    }
}
