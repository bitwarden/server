using Bit.Scim.Models;
using Xunit;

namespace Bit.Scim.Test.Models;

public class ScimFilterHelperTests
{
    [Theory]
    [InlineData("userName eq \"user@example.com\"", "username", "user@example.com")]
    [InlineData("userName eq user@example.com", "username", "user@example.com")]
    [InlineData("externalId eq \"abc-123\"", "externalid", "abc-123")]
    [InlineData("displayName eq \"My Group\"", "displayname", "My Group")]
    [InlineData("displayName eq My Group", "displayname", "My Group")]
    public void TryParseEqualityFilter_SupportedExpression_Parses(string filter, string expectedAttribute, string expectedValue)
    {
        var result = ScimFilterHelper.TryParseEqualityFilter(filter, out var attribute, out var value);

        Assert.True(result);
        Assert.Equal(expectedAttribute, attribute);
        Assert.Equal(expectedValue, value);
    }

    [Theory]
    [InlineData("active eq true")]
    [InlineData("userName pr")]
    [InlineData("userName sw \"a\"")]
    [InlineData("userName eq \"a\" and active eq true")]
    [InlineData("userName eq \"a\" or externalId eq \"b\"")]
    [InlineData("not (userName eq \"a\")")]
    [InlineData("emails[type eq \"work\"].value eq \"a\"")]
    [InlineData("")]
    [InlineData("   ")]
    public void TryParseEqualityFilter_UnsupportedExpression_ReturnsFalse(string filter)
    {
        var result = ScimFilterHelper.TryParseEqualityFilter(filter, out var attribute, out var value);

        Assert.False(result);
        Assert.Null(attribute);
        Assert.Null(value);
    }

    [Fact]
    public void GetUnsupportedFilterMessage_SingleAttribute_ReferencesAttribute()
    {
        var message = ScimFilterHelper.GetUnsupportedFilterMessage("active eq true");

        Assert.Equal("Filter attribute 'active' is not supported.", message);
    }

    [Fact]
    public void GetUnsupportedFilterMessage_CompoundExpression_ReferencesExpression()
    {
        var filter = "userName eq \"a\" and active eq true";

        var message = ScimFilterHelper.GetUnsupportedFilterMessage(filter);

        Assert.Equal($"Filter expression '{filter}' is not supported.", message);
    }
}
