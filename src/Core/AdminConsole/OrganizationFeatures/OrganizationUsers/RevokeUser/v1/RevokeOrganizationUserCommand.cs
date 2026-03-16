using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;
using Bit.Core.Services;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.RevokeUser.v1;

public class RevokeOrganizationUserCommand(
    IEventService eventService,
    IPushNotificationService pushNotificationService,
    IOrganizationUserRepository organizationUserRepository,
    ICurrentContext currentContext,
    IHasConfirmedOwnersExceptQuery hasConfirmedOwnersExceptQuery)
    : IRevokeOrganizationUserCommand
{
    public async Task RevokeUserAsync(OrganizationUser organizationUser, Guid? revokingUserId)
    {
        if (revokingUserId.HasValue && organizationUser.UserId == revokingUserId.Value)
        {
            throw new BadRequestException("You cannot revoke yourself.");
        }

        if (organizationUser.Type == OrganizationUserType.Owner && revokingUserId.HasValue &&
            !await currentContext.OrganizationOwner(organizationUser.OrganizationId))
        {
            throw new BadRequestException("Only owners can revoke other owners.");
        }

        await RepositoryRevokeUserAsync(organizationUser);
        await eventService.LogOrganizationUserEventAsync(organizationUser, EventType.OrganizationUser_Revoked);

        if (organizationUser.UserId.HasValue)
        {
            await pushNotificationService.PushSyncOrgKeysAsync(organizationUser.UserId.Value);
        }
    }

    public async Task RevokeUserAsync(OrganizationUser organizationUser,
        EventSystemUser systemUser)
    {
        await RepositoryRevokeUserAsync(organizationUser);
        await eventService.LogOrganizationUserEventAsync(organizationUser, EventType.OrganizationUser_Revoked,
            systemUser);

        if (organizationUser.UserId.HasValue)
        {
            await pushNotificationService.PushSyncOrgKeysAsync(organizationUser.UserId.Value);
        }
    }

    private async Task RepositoryRevokeUserAsync(OrganizationUser organizationUser)
    {
        if (organizationUser.Status == OrganizationUserStatusType.Revoked)
        {
            throw new BadRequestException("Already revoked.");
        }

        if (!await hasConfirmedOwnersExceptQuery.HasConfirmedOwnersExceptAsync(organizationUser.OrganizationId,
                new[] { organizationUser.Id }, includeProvider: true))
        {
            throw new BadRequestException("Organization must have at least one confirmed owner.");
        }

        await organizationUserRepository.RevokeAsync(organizationUser.Id);
        organizationUser.Status = OrganizationUserStatusType.Revoked;
    }
}
