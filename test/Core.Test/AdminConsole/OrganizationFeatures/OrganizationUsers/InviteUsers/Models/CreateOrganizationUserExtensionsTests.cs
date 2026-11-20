using Bit.Core.AdminConsole.Models.Business;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Models;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Models;

public class CreateOrganizationUserExtensionsTests
{
    [Theory, BitAutoData]
    public void MapToDataModel_CreatesInvitedOrganizationUser(Guid organizationId, DateTimeOffset performedAt)
    {
        var invite = CreateInvite("Invitee@Example.com", OrganizationUserType.User, "external-id");

        var result = invite.MapToDataModel(performedAt, new InviteOrganization { OrganizationId = organizationId });

        var orgUser = result.OrganizationUser;
        Assert.NotEqual(Guid.Empty, orgUser.Id);
        Assert.Equal(organizationId, orgUser.OrganizationId);
        Assert.Equal(OrganizationUserStatusType.Invited, orgUser.Status);
        Assert.Equal(OrganizationUserType.User, orgUser.Type);
        Assert.Equal("invitee@example.com", orgUser.Email);
        Assert.Null(orgUser.UserId);
        Assert.Null(orgUser.Key);
        Assert.Equal("external-id", orgUser.ExternalId);
        Assert.True(orgUser.AccessSecretsManager);
        Assert.Equal(performedAt.UtcDateTime, orgUser.CreationDate);
        Assert.Equal(performedAt.UtcDateTime, orgUser.RevisionDate);
        Assert.Null(orgUser.Permissions);
    }

    [Theory]
    [BitAutoData("")]
    [BitAutoData(" ")]
    public void MapToDataModel_BlankExternalId_SetsExternalIdToNull(string externalId, Guid organizationId,
        DateTimeOffset performedAt)
    {
        var invite = CreateInvite("invitee@example.com", OrganizationUserType.User, externalId);

        var result = invite.MapToDataModel(performedAt, new InviteOrganization { OrganizationId = organizationId });

        Assert.Null(result.OrganizationUser.ExternalId);
    }

    [Theory, BitAutoData]
    public void MapToDataModel_CustomType_SetsPermissions(Guid organizationId, DateTimeOffset performedAt)
    {
        var invite = CreateInvite("invitee@example.com", OrganizationUserType.Custom, null,
            new Permissions { AccessEventLogs = true });

        var result = invite.MapToDataModel(performedAt, new InviteOrganization { OrganizationId = organizationId });

        Assert.True(result.OrganizationUser.GetPermissions()?.AccessEventLogs);
    }

    private static OrganizationUserInviteCommandModel CreateInvite(string email, OrganizationUserType type,
        string? externalId, Permissions? permissions = null) =>
        new(email, [], [], type, permissions!, externalId!, accessSecretsManager: true);
}
