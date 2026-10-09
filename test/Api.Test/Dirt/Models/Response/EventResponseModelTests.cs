using Bit.Api.Dirt.Models.Response;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Api.Test.Dirt.Models.Response;

public class EventResponseModelTests
{
    [Theory]
    [BitAutoData(EventType.Secret_Retrieved)]
    [BitAutoData(EventType.Project_Retrieved)]
    public void Constructor_SecretOrProjectEvent_MissingActingUser_FallsBackToUserId(
        EventType type, Guid userId)
    {
        var ev = new EventMessage { Type = type, UserId = userId, ActingUserId = null };

        var response = new EventResponseModel(ev);

        Assert.Equal(userId, response.ActingUserId);
        // Unlike the public model, UserId is exposed here and must be left as stored.
        Assert.Equal(userId, response.UserId);
    }

    // ActingUserId-present and machine-account non-attribution are exercised once, on the public
    // model (EventResponseModelTests in Bit.Api.Test.Dirt.Public.Models) -- ResolveActingUserId is
    // the same call in both models, and neither of those cases touches UserId, so they'd be exact
    // duplicates here.

    /// <summary>
    /// ServiceAccount_UserAdded is deliberately excluded here: on that type UserId is a legacy
    /// stashed OrganizationUser id, so its ActingUserId non-fallback is covered instead by
    /// Constructor_LegacyServiceAccountPeopleEvent_MissingOrganizationUserId_ReclaimsUserIdAndHidesIt.
    /// </summary>
    [Theory]
    [BitAutoData(EventType.Send_Accessed_Text)]
    [BitAutoData(EventType.OrganizationUser_Invited)]
    public void Constructor_OtherEventTypes_MissingActingUser_DoesNotFallBack(
        EventType type, Guid userId)
    {
        var ev = new EventMessage { Type = type, UserId = userId, ActingUserId = null };

        var response = new EventResponseModel(ev);

        Assert.Null(response.ActingUserId);
        Assert.Equal(userId, response.UserId);
    }

    /// <summary>
    /// Legacy ServiceAccount_UserAdded/Removed rows stashed the granted member's OrganizationUser
    /// id in UserId. OrganizationUserId reclaims it, and UserId must stop showing the same value
    /// under the wrong label once it has been reclaimed.
    /// </summary>
    [Theory]
    [BitAutoData(EventType.ServiceAccount_UserAdded)]
    [BitAutoData(EventType.ServiceAccount_UserRemoved)]
    public void Constructor_LegacyServiceAccountPeopleEvent_MissingOrganizationUserId_ReclaimsUserIdAndHidesIt(
        EventType type, Guid organizationUserId)
    {
        var ev = new EventMessage { Type = type, UserId = organizationUserId, OrganizationUserId = null };

        var response = new EventResponseModel(ev);

        Assert.Equal(organizationUserId, response.OrganizationUserId);
        Assert.Null(response.UserId);
    }

    [Theory]
    [BitAutoData(EventType.ServiceAccount_UserAdded)]
    [BitAutoData(EventType.ServiceAccount_UserRemoved)]
    public void Constructor_ServiceAccountPeopleEvent_OrganizationUserIdPresent_DoesNotOverwriteEitherField(
        EventType type, Guid userId, Guid organizationUserId)
    {
        var ev = new EventMessage { Type = type, UserId = userId, OrganizationUserId = organizationUserId };

        var response = new EventResponseModel(ev);

        Assert.Equal(organizationUserId, response.OrganizationUserId);
        Assert.Equal(userId, response.UserId);
    }

    [Theory]
    [BitAutoData(EventType.ServiceAccount_GroupAdded)]
    public void Constructor_OtherServiceAccountEvents_MissingOrganizationUserId_DoesNotFallBack(
        EventType type, Guid userId)
    {
        var ev = new EventMessage { Type = type, UserId = userId, OrganizationUserId = null };

        var response = new EventResponseModel(ev);

        Assert.Null(response.OrganizationUserId);
        Assert.Equal(userId, response.UserId);
    }
}
