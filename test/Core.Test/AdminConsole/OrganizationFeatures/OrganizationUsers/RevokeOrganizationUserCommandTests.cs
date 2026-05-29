using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.RevokeUser.v1;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Test.AutoFixture.OrganizationUserFixtures;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.OrganizationUsers;

[SutProviderCustomize]
public class RevokeOrganizationUserCommandTests
{

    [Theory, BitAutoData]
    public async Task RevokeUser_Success(
        Organization organization,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.Owner)] OrganizationUser owner,
        [OrganizationUser] OrganizationUser organizationUser,
        SutProvider<RevokeOrganizationUserCommand> sutProvider)
    {
        RestoreRevokeUser_Setup(organization, owner, organizationUser, sutProvider);

        await sutProvider.Sut.RevokeUserAsync(organizationUser, owner.Id, RevocationReason.Manual);

        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .Received(1)
            .RevokeAsync(organizationUser.Id, RevocationReason.Manual);
        await sutProvider.GetDependency<IEventService>()
            .Received(1)
            .LogOrganizationUserEventAsync(organizationUser, EventType.OrganizationUser_Revoked);
        await sutProvider.GetDependency<IPushNotificationService>()
            .Received(1)
            .PushSyncOrgKeysAsync(organizationUser.UserId!.Value);
    }

    [Theory, BitAutoData]
    public async Task RevokeUser_CustomUserRevokeAdmin_Fails(
        Organization organization,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.Custom)] OrganizationUser customUser,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.Admin)] OrganizationUser organizationUser,
        SutProvider<RevokeOrganizationUserCommand> sutProvider)
    {
        // Arrange
        RestoreRevokeUser_Setup(organization, customUser, organizationUser, sutProvider);

        // Act
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.RevokeUserAsync(organizationUser, customUser.Id, RevocationReason.Manual));

        // Assert
        Assert.Contains("custom users can not revoke admins", exception.Message.ToLowerInvariant());
        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .DidNotReceiveWithAnyArgs()
            .RevokeAsync(Arg.Any<Guid>(), Arg.Any<RevocationReason>());
    }

    [Theory, BitAutoData]
    public async Task RevokeUser_AdminRevokeAdmin_Success(
        Organization organization,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.Admin)] OrganizationUser admin,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.Admin)] OrganizationUser organizationUser,
        SutProvider<RevokeOrganizationUserCommand> sutProvider)
    {
        // Arrange
        RestoreRevokeUser_Setup(organization, admin, organizationUser, sutProvider);

        // Act
        await sutProvider.Sut.RevokeUserAsync(organizationUser, admin.Id, RevocationReason.Manual);

        // Assert
        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .Received(1)
            .RevokeAsync(organizationUser.Id, RevocationReason.Manual);
    }

    [Theory, BitAutoData]
    public async Task RevokeUser_WithEventSystemUser_Success(
        Organization organization,
        [OrganizationUser] OrganizationUser organizationUser,
        EventSystemUser eventSystemUser,
        SutProvider<RevokeOrganizationUserCommand> sutProvider)
    {
        RestoreRevokeUser_Setup(organization, null, organizationUser, sutProvider);

        await sutProvider.Sut.RevokeUserAsync(organizationUser, eventSystemUser, RevocationReason.Manual);

        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .Received(1)
            .RevokeAsync(organizationUser.Id, RevocationReason.Manual);
        await sutProvider.GetDependency<IEventService>()
            .Received(1)
            .LogOrganizationUserEventAsync(organizationUser, EventType.OrganizationUser_Revoked, eventSystemUser);
        await sutProvider.GetDependency<IPushNotificationService>()
            .Received(1)
            .PushSyncOrgKeysAsync(organizationUser.UserId!.Value);
    }

    private void RestoreRevokeUser_Setup(
        Organization organization,
        OrganizationUser? requestingOrganizationUser,
        OrganizationUser targetOrganizationUser,
        SutProvider<RevokeOrganizationUserCommand> sutProvider)
    {
        if (requestingOrganizationUser != null)
        {
            requestingOrganizationUser.OrganizationId = organization.Id;
        }
        targetOrganizationUser.OrganizationId = organization.Id;

        sutProvider.GetDependency<ICurrentContext>().OrganizationOwner(organization.Id).Returns(requestingOrganizationUser != null && requestingOrganizationUser.Type is OrganizationUserType.Owner);
        sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(organization.Id).Returns(requestingOrganizationUser != null && requestingOrganizationUser.Type is OrganizationUserType.Owner or OrganizationUserType.Admin);
        sutProvider.GetDependency<IHasConfirmedOwnersExceptQuery>()
            .HasConfirmedOwnersExceptAsync(organization.Id, Arg.Any<IEnumerable<Guid>>())
            .Returns(true);
    }
}
