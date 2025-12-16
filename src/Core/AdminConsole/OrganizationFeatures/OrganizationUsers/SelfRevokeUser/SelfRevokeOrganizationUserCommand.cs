using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;
using Bit.Core.Services;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.SelfRevokeUser;

public class SelfRevokeOrganizationUserCommand(
    IOrganizationUserRepository organizationUserRepository,
    IPolicyRepository policyRepository,
    IEventService eventService,
    IPushNotificationService pushNotificationService)
    : ISelfRevokeOrganizationUserCommand
{
    public async Task SelfRevokeUserAsync(Guid organizationId, Guid userId)
    {
        var policy = await policyRepository.GetByOrganizationIdTypeAsync(organizationId, PolicyType.OrganizationDataOwnership);
        if (policy is not { Enabled: true })
        {
            throw new BadRequestException("Organization data ownership policy is not enabled.");
        }

        var organizationUser = await organizationUserRepository.GetByOrganizationAsync(organizationId, userId);
        if (organizationUser == null)
        {
            throw new NotFoundException();
        }

        if (organizationUser.Type is OrganizationUserType.Owner or OrganizationUserType.Admin)
        {
            throw new BadRequestException("Owners and Admins are exempt from the organization data ownership policy.");
        }

        if (organizationUser.Status is not OrganizationUserStatusType.Confirmed)
        {
            throw new BadRequestException("User must be confirmed to self-revoke.");
        }

        await organizationUserRepository.RevokeAsync(organizationUser.Id);
        await eventService.LogOrganizationUserEventAsync(organizationUser, EventType.OrganizationUser_SelfRevoked);
        await pushNotificationService.PushSyncOrgKeysAsync(organizationUser.UserId!.Value);
    }
}
