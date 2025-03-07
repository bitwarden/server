using Bit.Core.Models.Data;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Core.Test.Utilities;

public class TemplateProcessorTests
{
    [Theory, BitAutoData]
    public void ReplaceTokens_ReplacesSingleToken(EventMessage eventMessage)
    {
        var template = "Event #Type# occurred.";
        var expected = $"Event {eventMessage.Type} occurred.";
        var result = TemplateProcessor.ReplaceTokens(template, eventMessage);

        Assert.Equal(expected, result);
    }

    [Theory, BitAutoData]
    public void ReplaceTokens_ReplacesMultipleTokens(EventMessage eventMessage)
    {
        var template = "Event #Type#, User (id: #UserId#).";
        var expected = $"Event {eventMessage.Type}, User (id: {eventMessage.UserId}).";
        var result = TemplateProcessor.ReplaceTokens(template, eventMessage);

        Assert.Equal(expected, result);
    }

    [Theory, BitAutoData]
    public void ReplaceTokens_LeavesUnknownTokensUnchanged(EventMessage eventMessage)
    {
        var template = "Event #Type#, User (id: #UserId#), Details: #UnknownKey#.";
        var expected = $"Event {eventMessage.Type}, User (id: {eventMessage.UserId}), Details: #UnknownKey#.";
        var result = TemplateProcessor.ReplaceTokens(template, eventMessage);

        Assert.Equal(expected, result);
    }

    [Theory, BitAutoData]
    public void ReplaceTokens_WithNullProperty_LeavesTokenUnchanged(EventMessage eventMessage)
    {
        eventMessage.UserId = null; // Ensure UserId is null for this test

        var template = "Event #Type#, User (id: #UserId#).";
        var expected = $"Event {eventMessage.Type}, User (id: #UserId#).";
        var result = TemplateProcessor.ReplaceTokens(template, eventMessage);

        Assert.Equal(expected, result);
    }

    [Theory, BitAutoData]
    public void ReplaceTokens_IgnoresCaseSensitiveTokens(EventMessage eventMessage)
    {
        var template = "Event #type#, User (id: #UserId#)."; // Lowercase "type"
        var expected = $"Event #type#, User (id: {eventMessage.UserId})."; // Token remains unchanged
        var result = TemplateProcessor.ReplaceTokens(template, eventMessage);

        Assert.Equal(expected, result);
    }

    [Theory, BitAutoData]
    public void ReplaceTokens_ReturnsOriginalString_IfNoTokensPresent(EventMessage eventMessage)
    {
        var template = "System is running normally.";
        var expected = "System is running normally.";
        var result = TemplateProcessor.ReplaceTokens(template, eventMessage);

        Assert.Equal(expected, result);
    }

    [Theory, BitAutoData]
    public void ReplaceTokens_ReturnsOriginalString_IfTemplateIsNullOrEmpty(EventMessage eventMessage)
    {
        var emptyTemplate = "";
        var expectedEmpty = "";

        Assert.Equal(expectedEmpty, TemplateProcessor.ReplaceTokens(emptyTemplate, eventMessage));
        Assert.Null(TemplateProcessor.ReplaceTokens(null, eventMessage));
    }

    [Fact]
    public void ReplaceTokens_ReturnsOriginalString_IfDataObjectIsNull()
    {
        var template = "Event #Type#, User (id: #UserId#).";
        var expected = "Event #Type#, User (id: #UserId#).";

        var result = TemplateProcessor.ReplaceTokens(template, null);

        Assert.Equal(expected, result);
    }
}
