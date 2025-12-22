using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;
using Bit.Core.Services;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.SelfRevokeUser;

public class SelfRevokeOrganizationUserCommand(
    IOrganizationUserRepository organizationUserRepository,
    IPolicyRequirementQuery policyRequirementQuery,
    IHasConfirmedOwnersExceptQuery hasConfirmedOwnersExceptQuery,
    IEventService eventService,
    IPushNotificationService pushNotificationService)
    : ISelfRevokeOrganizationUserCommand
{
    public async Task SelfRevokeUserAsync(Guid organizationId, Guid userId)
    {
        var organizationUser = await organizationUserRepository.GetByOrganizationAsync(organizationId, userId);
        if (organizationUser == null)
        {
            throw new NotFoundException();
        }

        var policyRequirement = await policyRequirementQuery.GetAsync<OrganizationDataOwnershipPolicyRequirement>(userId);

        if (!policyRequirement.EligibleForSelfRevoke(organizationId))
        {
            throw new BadRequestException("User is not eligible for self-revocation. The organization data ownership policy must be enabled and the user must be a confirmed member.");
        }

        // Prevent the last owner from revoking themselves, which would brick the organization
        if (organizationUser.Type == OrganizationUserType.Owner)
        {
            var hasOtherOwner = await hasConfirmedOwnersExceptQuery.HasConfirmedOwnersExceptAsync(
                organizationId,
                [organizationUser.Id],
                includeProvider: true);

            if (!hasOtherOwner)
            {
                throw new BadRequestException("The last owner cannot revoke themselves.");
            }
        }

        await organizationUserRepository.RevokeAsync(organizationUser.Id);
        await eventService.LogOrganizationUserEventAsync(organizationUser, EventType.OrganizationUser_SelfRevoked);
        await pushNotificationService.PushSyncOrgKeysAsync(organizationUser.UserId!.Value);
    }
}
