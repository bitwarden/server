#nullable enable

using Bit.Core.AdminConsole.Utilities;
using Bit.Core.Models.Data;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Core.Test.AdminConsole.Utilities;

public class IntegrationTemplateProcessorTests
{
    [Theory, BitAutoData]
    public void ReplaceTokens_ReplacesSingleToken(EventMessage eventMessage)
    {
        var template = "Event #Type# occurred.";
        var expected = $"Event {eventMessage.Type} occurred.";
        var result = IntegrationTemplateProcessor.ReplaceTokens(template, eventMessage);

        Assert.Equal(expected, result);
    }

    [Theory, BitAutoData]
    public void ReplaceTokens_ReplacesMultipleTokens(EventMessage eventMessage)
    {
        var template = "Event #Type#, User (id: #UserId#).";
        var expected = $"Event {eventMessage.Type}, User (id: {eventMessage.UserId}).";
        var result = IntegrationTemplateProcessor.ReplaceTokens(template, eventMessage);

        Assert.Equal(expected, result);
    }

    [Theory, BitAutoData]
    public void ReplaceTokens_LeavesUnknownTokensUnchanged(EventMessage eventMessage)
    {
        var template = "Event #Type#, User (id: #UserId#), Details: #UnknownKey#.";
        var expected = $"Event {eventMessage.Type}, User (id: {eventMessage.UserId}), Details: #UnknownKey#.";
        var result = IntegrationTemplateProcessor.ReplaceTokens(template, eventMessage);

        Assert.Equal(expected, result);
    }

    [Theory, BitAutoData]
    public void ReplaceTokens_WithNullProperty_InsertsEmptyString(EventMessage eventMessage)
    {
        eventMessage.UserId = null;

        var template = "Event #Type#, User (id: #UserId#).";
        var expected = $"Event {eventMessage.Type}, User (id: ).";
        var result = IntegrationTemplateProcessor.ReplaceTokens(template, eventMessage);

        Assert.Equal(expected, result);
    }

    [Theory, BitAutoData]
    public void ReplaceTokens_TokensWithNonmatchingCase_LeavesTokensUnchanged(EventMessage eventMessage)
    {
        var template = "Event #type#, User (id: #UserId#).";
        var expected = $"Event #type#, User (id: {eventMessage.UserId}).";
        var result = IntegrationTemplateProcessor.ReplaceTokens(template, eventMessage);

        Assert.Equal(expected, result);
    }

    [Theory, BitAutoData]
    public void ReplaceTokens_NoTokensPresent_ReturnsOriginalString(EventMessage eventMessage)
    {
        var template = "System is running normally.";
        var expected = "System is running normally.";
        var result = IntegrationTemplateProcessor.ReplaceTokens(template, eventMessage);

        Assert.Equal(expected, result);
    }

    [Theory, BitAutoData]
    public void ReplaceTokens_TemplateIsEmpty_ReturnsOriginalString(EventMessage eventMessage)
    {
        var emptyTemplate = "";
        var expectedEmpty = "";

        Assert.Equal(expectedEmpty, IntegrationTemplateProcessor.ReplaceTokens(emptyTemplate, eventMessage));
    }

    [Theory]
    [InlineData("User name is #UserName#")]
    [InlineData("Email: #UserEmail#")]
    public void TemplateRequiresUser_ContainingKeys_ReturnsTrue(string template)
    {
        var result = IntegrationTemplateProcessor.TemplateRequiresUser(template);
        Assert.True(result);
    }

    [Theory]
    [InlineData("#UserId#")]  // This is on the base class, not fetched, so should be false
    [InlineData("No User Tokens")]
    [InlineData("")]
    public void TemplateRequiresUser_EmptyInputOrNoMatchingKeys_ReturnsFalse(string template)
    {
        var result = IntegrationTemplateProcessor.TemplateRequiresUser(template);
        Assert.False(result);
    }

    [Theory]
    [InlineData("Acting user is #ActingUserName#")]
    [InlineData("Acting user's email is #ActingUserEmail#")]
    public void TemplateRequiresActingUser_ContainingKeys_ReturnsTrue(string template)
    {
        var result = IntegrationTemplateProcessor.TemplateRequiresActingUser(template);
        Assert.True(result);
    }

    [Theory]
    [InlineData("No ActiveUser tokens")]
    [InlineData("#ActiveUserId#")]  // This is on the base class, not fetched, so should be false
    [InlineData("")]
    public void TemplateRequiresActingUser_EmptyInputOrNoMatchingKeys_ReturnsFalse(string template)
    {
        var result = IntegrationTemplateProcessor.TemplateRequiresActingUser(template);
        Assert.False(result);
    }

    [Theory]
    [InlineData("Group name is #GroupName#!")]
    [InlineData("Group: #GroupName#")]
    public void TemplateRequiresGroup_ContainingKeys_ReturnsTrue(string template)
    {
        var result = IntegrationTemplateProcessor.TemplateRequiresGroup(template);
        Assert.True(result);
    }

    [Theory]
    [InlineData("#GroupId#")]  // This is on the base class, not fetched, so should be false
    [InlineData("No Group Tokens")]
    [InlineData("")]
    public void TemplateRequiresGroup_EmptyInputOrNoMatchingKeys_ReturnsFalse(string template)
    {
        var result = IntegrationTemplateProcessor.TemplateRequiresGroup(template);
        Assert.False(result);
    }

    [Theory]
    [InlineData("Organization: #OrganizationName#")]
    [InlineData("Welcome to #OrganizationName#")]
    public void TemplateRequiresOrganization_ContainingKeys_ReturnsTrue(string template)
    {
        var result = IntegrationTemplateProcessor.TemplateRequiresOrganization(template);
        Assert.True(result);
    }

    [Theory]
    [InlineData("No organization tokens")]
    [InlineData("#OrganizationId#")]  // This is on the base class, not fetched, so should be false
    [InlineData("")]
    public void TemplateRequiresOrganization_EmptyInputOrNoMatchingKeys_ReturnsFalse(string template)
    {
        var result = IntegrationTemplateProcessor.TemplateRequiresOrganization(template);
        Assert.False(result);
    }
}
