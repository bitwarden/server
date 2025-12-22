using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.AdminConsole.Utilities.v2.Results;
using Bit.Core.Enums;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;
using Bit.Core.Services;
using OneOf.Types;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.SelfRevokeUser;

public class SelfRevokeOrganizationUserCommand(
    IOrganizationUserRepository organizationUserRepository,
    IPolicyRequirementQuery policyRequirementQuery,
    IHasConfirmedOwnersExceptQuery hasConfirmedOwnersExceptQuery,
    IEventService eventService,
    IPushNotificationService pushNotificationService)
    : ISelfRevokeOrganizationUserCommand
{
    public async Task<CommandResult> SelfRevokeUserAsync(Guid organizationId, Guid userId)
    {
        var organizationUser = await organizationUserRepository.GetByOrganizationAsync(organizationId, userId);
        if (organizationUser == null)
        {
            return new OrganizationUserNotFound();
        }

        var policyRequirement = await policyRequirementQuery.GetAsync<OrganizationDataOwnershipPolicyRequirement>(userId);

        if (!policyRequirement.EligibleForSelfRevoke(organizationId))
        {
            return new NotEligibleForSelfRevoke();
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
                return new LastOwnerCannotSelfRevoke();
            }
        }

        await organizationUserRepository.RevokeAsync(organizationUser.Id);
        await eventService.LogOrganizationUserEventAsync(organizationUser, EventType.OrganizationUser_SelfRevoked);
        await pushNotificationService.PushSyncOrgKeysAsync(organizationUser.UserId!.Value);

        return new None();
    }
}
