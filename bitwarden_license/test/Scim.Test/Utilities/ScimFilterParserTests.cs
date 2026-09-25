using Bit.Scim.Utilities;
using Xunit;

namespace Bit.Scim.Test.Utilities;

public class ScimFilterParserTests
{
    [Theory]
    [InlineData("userName eq \"john\"", "username", "eq", "john")]
    [InlineData("userName ne \"john\"", "username", "ne", "john")]
    [InlineData("userName co \"john\"", "username", "co", "john")]
    [InlineData("userName sw \"john\"", "username", "sw", "john")]
    [InlineData("externalId eq \"abc123\"", "externalid", "eq", "abc123")]
    [InlineData("displayName eq \"Test Group\"", "displayname", "eq", "Test Group")]
    public void Parse_ValidFilter_ReturnsTrue(string filter, string expectedAttr, string expectedOp, string expectedValue)
    {
        var result = ScimFilterParser.Parse(filter, out var attribute, out var op, out var value);

        Assert.True(result);
        Assert.Equal(expectedAttr, attribute);
        Assert.Equal(expectedOp, op);
        Assert.Equal(expectedValue, value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("invalidfilter")]
    public void Parse_InvalidFilter_ReturnsFalse(string filter)
    {
        var result = ScimFilterParser.Parse(filter, out var attribute, out var op, out var value);

        Assert.False(result);
        Assert.Null(attribute);
        Assert.Null(op);
        Assert.Null(value);
    }

    [Theory]
    [InlineData("hello", "eq", "hello", true)]
    [InlineData("hello", "eq", "world", false)]
    [InlineData("hello", "ne", "world", true)]
    [InlineData("hello", "ne", "hello", false)]
    [InlineData("hello world", "co", "lo wo", true)]
    [InlineData("hello", "co", "xyz", false)]
    [InlineData("hello", "sw", "hel", true)]
    [InlineData("hello", "sw", "xyz", false)]
    public void Matches_WithValue_ReturnsExpected(string fieldValue, string op, string filterValue, bool expected)
    {
        var result = ScimFilterParser.Matches(fieldValue, op, filterValue);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void Matches_NullFieldValue_Eq_ReturnsFalse()
    {
        Assert.False(ScimFilterParser.Matches(null, "eq", "value"));
    }

    [Fact]
    public void Matches_NullFieldValue_Ne_ReturnsTrue()
    {
        Assert.True(ScimFilterParser.Matches(null, "ne", "value"));
    }

    [Fact]
    public void Parse_NullFilter_ReturnsFalse()
    {
        var result = ScimFilterParser.Parse(null!, out var attribute, out var op, out var value);

        Assert.False(result);
    }

    [Fact]
    public void Matches_CaseInsensitive()
    {
        Assert.True(ScimFilterParser.Matches("Hello", "eq", "hello"));
        Assert.True(ScimFilterParser.Matches("Hello World", "co", "LO WO"));
        Assert.True(ScimFilterParser.Matches("Hello", "sw", "HEL"));
    }
}
