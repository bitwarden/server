using System.Text.Json;
using Bit.Services.Pam.Enums;
using Xunit;

namespace Bit.Services.Pam.Test.Models.Conditions;

public class AccessWeekdayJsonConverterTests
{
    [Theory]
    [InlineData(AccessWeekday.Sun, "\"sun\"")]
    [InlineData(AccessWeekday.Mon, "\"mon\"")]
    [InlineData(AccessWeekday.Tue, "\"tue\"")]
    [InlineData(AccessWeekday.Wed, "\"wed\"")]
    [InlineData(AccessWeekday.Thu, "\"thu\"")]
    [InlineData(AccessWeekday.Fri, "\"fri\"")]
    [InlineData(AccessWeekday.Sat, "\"sat\"")]
    public void Serializes_AsLowercaseToken(AccessWeekday day, string expectedJson)
    {
        Assert.Equal(expectedJson, JsonSerializer.Serialize(day));
    }

    [Theory]
    [InlineData("\"mon\"", AccessWeekday.Mon)]
    [InlineData("\"MON\"", AccessWeekday.Mon)]
    [InlineData("\"Sun\"", AccessWeekday.Sun)]
    public void Deserializes_TokenCaseInsensitively(string json, AccessWeekday expected)
    {
        Assert.Equal(expected, JsonSerializer.Deserialize<AccessWeekday>(json));
    }

    [Theory]
    [InlineData("\"funday\"")]
    [InlineData("\"\"")]
    [InlineData("3")]
    public void Deserialize_InvalidToken_Throws(string json)
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<AccessWeekday>(json));
    }

    [Fact]
    public void EnumValues_AlignWithSystemDayOfWeek()
    {
        // The engine casts AccessWeekday straight to System.DayOfWeek, so the numeric values must match.
        Assert.Equal((int)DayOfWeek.Sunday, (int)AccessWeekday.Sun);
        Assert.Equal((int)DayOfWeek.Monday, (int)AccessWeekday.Mon);
        Assert.Equal((int)DayOfWeek.Tuesday, (int)AccessWeekday.Tue);
        Assert.Equal((int)DayOfWeek.Wednesday, (int)AccessWeekday.Wed);
        Assert.Equal((int)DayOfWeek.Thursday, (int)AccessWeekday.Thu);
        Assert.Equal((int)DayOfWeek.Friday, (int)AccessWeekday.Fri);
        Assert.Equal((int)DayOfWeek.Saturday, (int)AccessWeekday.Sat);
    }
}
