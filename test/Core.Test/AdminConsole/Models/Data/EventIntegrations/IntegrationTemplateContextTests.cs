#nullable enable
using System.Text.Json;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Models.Data.EventIntegrations;
using Bit.Core.Models.Data;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Core.Test.AdminConsole.Models.Data.EventIntegrations;

public class IntegrationTemplateContextTests
{
    [Theory, BitAutoData]
    public void EventMessage_ReturnsSerializedJsonOfEvent(EventMessage eventMessage)
    {
        var sut = new IntegrationTemplateContext(eventMessage: eventMessage);
        var expected = JsonSerializer.Serialize(eventMessage);

        Assert.Equal(expected, sut.EventMessage);
    }

    [Theory, BitAutoData]
    public void DateIso8601_ReturnsIso8601FormattedDate(EventMessage eventMessage)
    {
        var testDate = new DateTime(2025, 10, 27, 13, 30, 0, DateTimeKind.Utc);
        eventMessage.Date = testDate;
        var sut = new IntegrationTemplateContext(eventMessage);

        var result = sut.DateIso8601;

        Assert.Equal("2025-10-27T13:30:00.0000000Z", result);
        // Verify it's valid ISO 8601
        Assert.True(DateTime.TryParse(result, out _));
    }

    [Theory, BitAutoData]
    public void UserName_WhenUserIsSet_ReturnsName(EventMessage eventMessage, OrganizationUserUserDetails user)
    {
        var sut = new IntegrationTemplateContext(eventMessage) { User = user };

        Assert.Equal(user.Name, sut.UserName);
    }

    [Theory, BitAutoData]
    public void UserName_WhenUserIsNull_ReturnsNull(EventMessage eventMessage)
    {
        var sut = new IntegrationTemplateContext(eventMessage) { User = null };

        Assert.Null(sut.UserName);
    }

    [Theory, BitAutoData]
    public void UserEmail_WhenUserIsSet_ReturnsEmail(EventMessage eventMessage, OrganizationUserUserDetails user)
    {
        var sut = new IntegrationTemplateContext(eventMessage) { User = user };

        Assert.Equal(user.Email, sut.UserEmail);
    }

    [Theory, BitAutoData]
    public void UserEmail_WhenUserIsNull_ReturnsNull(EventMessage eventMessage)
    {
        var sut = new IntegrationTemplateContext(eventMessage) { User = null };

        Assert.Null(sut.UserEmail);
    }

    [Theory, BitAutoData]
    public void UserType_WhenUserIsSet_ReturnsType(EventMessage eventMessage, OrganizationUserUserDetails user)
    {
        var sut = new IntegrationTemplateContext(eventMessage) { User = user };

        Assert.Equal(user.Type, sut.UserType);
    }

    [Theory, BitAutoData]
    public void UserType_WhenUserIsNull_ReturnsNull(EventMessage eventMessage)
    {
        var sut = new IntegrationTemplateContext(eventMessage) { User = null };

        Assert.Null(sut.UserType);
    }

    [Theory, BitAutoData]
    public void ActingUserName_WhenActingUserIsSet_ReturnsName(EventMessage eventMessage, OrganizationUserUserDetails actingUser)
    {
        var sut = new IntegrationTemplateContext(eventMessage) { ActingUser = actingUser };

        Assert.Equal(actingUser.Name, sut.ActingUserName);
    }

    [Theory, BitAutoData]
    public void ActingUserName_WhenActingUserIsNull_ReturnsNull(EventMessage eventMessage)
    {
        var sut = new IntegrationTemplateContext(eventMessage) { ActingUser = null };

        Assert.Null(sut.ActingUserName);
    }

    [Theory, BitAutoData]
    public void ActingUserEmail_WhenActingUserIsSet_ReturnsEmail(EventMessage eventMessage, OrganizationUserUserDetails actingUser)
    {
        var sut = new IntegrationTemplateContext(eventMessage) { ActingUser = actingUser };

        Assert.Equal(actingUser.Email, sut.ActingUserEmail);
    }

    [Theory, BitAutoData]
    public void ActingUserEmail_WhenActingUserIsNull_ReturnsNull(EventMessage eventMessage)
    {
        var sut = new IntegrationTemplateContext(eventMessage) { ActingUser = null };

        Assert.Null(sut.ActingUserEmail);
    }

    [Theory, BitAutoData]
    public void ActingUserType_WhenActingUserIsSet_ReturnsType(EventMessage eventMessage, OrganizationUserUserDetails actingUser)
    {
        var sut = new IntegrationTemplateContext(eventMessage) { ActingUser = actingUser };

        Assert.Equal(actingUser.Type, sut.ActingUserType);
    }

    [Theory, BitAutoData]
    public void ActingUserType_WhenActingUserIsNull_ReturnsNull(EventMessage eventMessage)
    {
        var sut = new IntegrationTemplateContext(eventMessage) { ActingUser = null };

        Assert.Null(sut.ActingUserType);
    }

    [Theory, BitAutoData]
    public void OrganizationName_WhenOrganizationIsSet_ReturnsDisplayName(EventMessage eventMessage, Organization organization)
    {
        var sut = new IntegrationTemplateContext(eventMessage) { Organization = organization };

        Assert.Equal(organization.DisplayName(), sut.OrganizationName);
    }

    [Theory, BitAutoData]
    public void OrganizationName_WhenOrganizationIsNull_ReturnsNull(EventMessage eventMessage)
    {
        var sut = new IntegrationTemplateContext(eventMessage) { Organization = null };

        Assert.Null(sut.OrganizationName);
    }

    [Theory, BitAutoData]
    public void GroupName_WhenGroupIsSet_ReturnsName(EventMessage eventMessage, Group group)
    {
        var sut = new IntegrationTemplateContext(eventMessage) { Group = group };

        Assert.Equal(group.Name, sut.GroupName);
    }

    [Theory, BitAutoData]
    public void GroupName_WhenGroupIsNull_ReturnsNull(EventMessage eventMessage)
    {
        var sut = new IntegrationTemplateContext(eventMessage) { Group = null };

        Assert.Null(sut.GroupName);
    }
}
