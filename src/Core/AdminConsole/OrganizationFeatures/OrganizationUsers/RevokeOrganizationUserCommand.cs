using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;
using Bit.Core.Services;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers;

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

    public async Task<List<Tuple<OrganizationUser, string>>> RevokeUsersAsync(Guid organizationId,
        IEnumerable<Guid> organizationUserIds, Guid? revokingUserId)
    {
        var orgUsers = await organizationUserRepository.GetManyAsync(organizationUserIds);
        var filteredUsers = orgUsers.Where(u => u.OrganizationId == organizationId)
            .ToList();

        if (!filteredUsers.Any())
        {
            throw new BadRequestException("Users invalid.");
        }

        if (!await hasConfirmedOwnersExceptQuery.HasConfirmedOwnersExceptAsync(organizationId, organizationUserIds))
        {
            throw new BadRequestException("Organization must have at least one confirmed owner.");
        }

        var deletingUserIsOwner = false;
        if (revokingUserId.HasValue)
        {
            deletingUserIsOwner = await currentContext.OrganizationOwner(organizationId);
        }

        var result = new List<Tuple<OrganizationUser, string>>();

        foreach (var organizationUser in filteredUsers)
        {
            try
            {
                if (organizationUser.Status == OrganizationUserStatusType.Revoked)
                {
                    throw new BadRequestException("Already revoked.");
                }

                if (revokingUserId.HasValue && organizationUser.UserId == revokingUserId)
                {
                    throw new BadRequestException("You cannot revoke yourself.");
                }

                if (organizationUser.Type == OrganizationUserType.Owner && revokingUserId.HasValue &&
                    !deletingUserIsOwner)
                {
                    throw new BadRequestException("Only owners can revoke other owners.");
                }

                await organizationUserRepository.RevokeAsync(organizationUser.Id);
                organizationUser.Status = OrganizationUserStatusType.Revoked;
                await eventService.LogOrganizationUserEventAsync(organizationUser, EventType.OrganizationUser_Revoked);
                if (organizationUser.UserId.HasValue)
                {
                    await pushNotificationService.PushSyncOrgKeysAsync(organizationUser.UserId.Value);
                }

                result.Add(Tuple.Create(organizationUser, ""));
            }
            catch (BadRequestException e)
            {
                result.Add(Tuple.Create(organizationUser, e.Message));
            }
        }

        return result;
    }
}
