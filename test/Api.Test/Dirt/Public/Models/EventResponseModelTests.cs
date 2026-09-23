using Bit.Api.Dirt.Public.Models;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Api.Test.Dirt.Public.Models;

public class EventResponseModelTests
{
    [Theory]
    [BitAutoData(EventType.Secret_Retrieved)]
    [BitAutoData(EventType.Secret_Restored)]
    [BitAutoData(EventType.Project_Retrieved)]
    [BitAutoData(EventType.Project_Deleted)]
    public void Constructor_SecretOrProjectEvent_MissingActingUser_FallsBackToUserId(
        EventType type, Guid userId)
    {
        var ev = new EventMessage { Type = type, UserId = userId, ActingUserId = null };

        var response = new EventResponseModel(ev);

        Assert.Equal(userId, response.ActingUserId);
    }

    [Theory]
    [BitAutoData(EventType.Secret_Created)]
    [BitAutoData(EventType.Project_Edited)]
    public void Constructor_SecretOrProjectEvent_ActingUserPresent_DoesNotOverwriteIt(
        EventType type, Guid userId, Guid actingUserId)
    {
        var ev = new EventMessage { Type = type, UserId = userId, ActingUserId = actingUserId };

        var response = new EventResponseModel(ev);

        Assert.Equal(actingUserId, response.ActingUserId);
    }

    [Theory]
    [BitAutoData(EventType.Secret_Retrieved)]
    [BitAutoData(EventType.Project_Retrieved)]
    public void Constructor_MachineAccountSecretOrProjectEvent_StaysUnattributed(
        EventType type, Guid serviceAccountId)
    {
        var ev = new EventMessage
        {
            Type = type,
            UserId = null,
            ActingUserId = null,
            ServiceAccountId = serviceAccountId
        };

        var response = new EventResponseModel(ev);

        Assert.Null(response.ActingUserId);
        Assert.Equal(serviceAccountId, response.ServiceAccountId);
    }

    /// <summary>
    /// On these types UserId is not the actor: the Send owner did not access their own Send, a
    /// SCIM-invited member did not invite themselves, and ServiceAccount_* rows store an
    /// OrganizationUser id in UserId. The fallback must not reach them.
    /// </summary>
    [Theory]
    [BitAutoData(EventType.Send_Accessed_Text)]
    [BitAutoData(EventType.Send_Accessed_File)]
    [BitAutoData(EventType.OrganizationUser_Invited)]
    [BitAutoData(EventType.ServiceAccount_UserAdded)]
    [BitAutoData(EventType.Cipher_Created)]
    public void Constructor_OtherEventTypes_MissingActingUser_DoesNotFallBack(
        EventType type, Guid userId)
    {
        var ev = new EventMessage { Type = type, UserId = userId, ActingUserId = null };

        var response = new EventResponseModel(ev);

        Assert.Null(response.ActingUserId);
    }

    [Theory]
    [BitAutoData(EventType.ServiceAccount_UserAdded)]
    [BitAutoData(EventType.ServiceAccount_UserRemoved)]
    public void Constructor_LegacyServiceAccountPeopleEvent_MissingOrganizationUserId_FallsBackToUserId(
        EventType type, Guid organizationUserId)
    {
        var ev = new EventMessage { Type = type, UserId = organizationUserId, OrganizationUserId = null };

        var response = new EventResponseModel(ev);

        Assert.Equal(organizationUserId, response.MemberId);
    }

    [Theory]
    [BitAutoData(EventType.ServiceAccount_UserAdded)]
    [BitAutoData(EventType.ServiceAccount_UserRemoved)]
    public void Constructor_ServiceAccountPeopleEvent_OrganizationUserIdPresent_DoesNotOverwriteIt(
        EventType type, Guid userId, Guid organizationUserId)
    {
        var ev = new EventMessage { Type = type, UserId = userId, OrganizationUserId = organizationUserId };

        var response = new EventResponseModel(ev);

        Assert.Equal(organizationUserId, response.MemberId);
    }

    /// <summary>
    /// ServiceAccount_GroupAdded/Removed and ServiceAccount_Created/Deleted never wrote to UserId,
    /// so a UserId that happens to be present on them is not a stashed OrganizationUser id and
    /// must not be picked up by the MemberId fallback.
    /// </summary>
    [Theory]
    [BitAutoData(EventType.ServiceAccount_GroupAdded)]
    [BitAutoData(EventType.ServiceAccount_Created)]
    public void Constructor_OtherServiceAccountEvents_MissingOrganizationUserId_DoesNotFallBack(
        EventType type, Guid userId)
    {
        var ev = new EventMessage { Type = type, UserId = userId, OrganizationUserId = null };

        var response = new EventResponseModel(ev);

        Assert.Null(response.MemberId);
    }
}
