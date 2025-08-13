// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.AdminConsole.Services;
using Bit.Core.Auth.UserFeatures.TwoFactorAuth.Interfaces;
using Bit.Core.Billing.Enums;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;
using Bit.Core.Services;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.RestoreUser.v1;

public class RestoreOrganizationUserCommand(
    ICurrentContext currentContext,
    IEventService eventService,
    IPushNotificationService pushNotificationService,
    IOrganizationUserRepository organizationUserRepository,
    IOrganizationRepository organizationRepository,
    ITwoFactorIsEnabledQuery twoFactorIsEnabledQuery,
    IPolicyService policyService,
    IUserRepository userRepository,
    IOrganizationService organizationService,
    IFeatureService featureService,
    IPolicyRequirementQuery policyRequirementQuery) : IRestoreOrganizationUserCommand
{
    public async Task RestoreUserAsync(OrganizationUser organizationUser, Guid? restoringUserId)
    {
        if (restoringUserId.HasValue && organizationUser.UserId == restoringUserId.Value)
        {
            throw new BadRequestException("You cannot restore yourself.");
        }

        if (organizationUser.Type == OrganizationUserType.Owner && restoringUserId.HasValue &&
            !await currentContext.OrganizationOwner(organizationUser.OrganizationId))
        {
            throw new BadRequestException("Only owners can restore other owners.");
        }

        await RepositoryRestoreUserAsync(organizationUser);
        await eventService.LogOrganizationUserEventAsync(organizationUser, EventType.OrganizationUser_Restored);

        if (organizationUser.UserId.HasValue)
        {
            await pushNotificationService.PushSyncOrgKeysAsync(organizationUser.UserId.Value);
        }
    }

    public async Task RestoreUserAsync(OrganizationUser organizationUser, EventSystemUser systemUser)
    {
        await RepositoryRestoreUserAsync(organizationUser);
        await eventService.LogOrganizationUserEventAsync(organizationUser, EventType.OrganizationUser_Restored,
            systemUser);

        if (organizationUser.UserId.HasValue)
        {
            await pushNotificationService.PushSyncOrgKeysAsync(organizationUser.UserId.Value);
        }
    }

    private async Task RepositoryRestoreUserAsync(OrganizationUser organizationUser)
    {
        if (organizationUser.Status != OrganizationUserStatusType.Revoked)
        {
            throw new BadRequestException("Already active.");
        }

        var organization = await organizationRepository.GetByIdAsync(organizationUser.OrganizationId);
        var seatCounts = await organizationRepository.GetOccupiedSeatCountByOrganizationIdAsync(organization.Id);
        var availableSeats = organization.Seats.GetValueOrDefault(0) - seatCounts.Total;

        if (availableSeats < 1)
        {
            await organizationService.AutoAddSeatsAsync(organization, 1); // Hooray
        }

        var userTwoFactorIsEnabled = false;
        // Only check 2FA status if the user is linked to a user account
        if (organizationUser.UserId.HasValue)
        {
            userTwoFactorIsEnabled =
                (await twoFactorIsEnabledQuery.TwoFactorIsEnabledAsync([organizationUser.UserId.Value]))
                .FirstOrDefault()
                .twoFactorIsEnabled;
        }

        if (organization.PlanType == PlanType.Free)
        {
            await CheckUserForOtherFreeOrganizationOwnershipAsync(organizationUser);
        }

        await CheckPoliciesBeforeRestoreAsync(organizationUser, userTwoFactorIsEnabled);

        var status = OrganizationService.GetPriorActiveOrganizationUserStatusType(organizationUser);

        await organizationUserRepository.RestoreAsync(organizationUser.Id, status);

        organizationUser.Status = status;
    }

    private async Task CheckUserForOtherFreeOrganizationOwnershipAsync(OrganizationUser organizationUser)
    {
        var relatedOrgUsersFromOtherOrgs = await organizationUserRepository.GetManyByUserAsync(organizationUser.UserId!.Value);
        var otherOrgs = await organizationRepository.GetManyByUserIdAsync(organizationUser.UserId.Value);

        var orgOrgUserDict = relatedOrgUsersFromOtherOrgs
            .Where(x => x.Id != organizationUser.Id)
            .ToDictionary(x => x, x => otherOrgs.FirstOrDefault(y => y.Id == x.OrganizationId));

        CheckForOtherFreeOrganizationOwnership(organizationUser, orgOrgUserDict);
    }

    private async Task<Dictionary<OrganizationUser, Organization>> GetRelatedOrganizationUsersAndOrganizationsAsync(
        List<OrganizationUser> organizationUsers)
    {
        var allUserIds = organizationUsers
            .Where(x => x.UserId.HasValue)
            .Select(x => x.UserId.Value);

        var otherOrganizationUsers = (await organizationUserRepository.GetManyByManyUsersAsync(allUserIds))
            .Where(x => organizationUsers.Any(y => y.Id == x.Id) == false)
            .ToArray();

        var otherOrgs = await organizationRepository.GetManyByIdsAsync(otherOrganizationUsers
                .Select(x => x.OrganizationId)
                .Distinct());

        return otherOrganizationUsers
            .ToDictionary(x => x, x => otherOrgs.FirstOrDefault(y => y.Id == x.OrganizationId));
    }

    private static void CheckForOtherFreeOrganizationOwnership(OrganizationUser organizationUser,
        Dictionary<OrganizationUser, Organization> otherOrgUsersAndOrgs)
    {
        var ownerOrAdminList = new[] { OrganizationUserType.Owner, OrganizationUserType.Admin };

        if (ownerOrAdminList.Any(x => organizationUser.Type == x) &&
            otherOrgUsersAndOrgs.Any(x =>
                x.Key.UserId == organizationUser.UserId &&
                ownerOrAdminList.Any(userType => userType == x.Key.Type) &&
                x.Key.Status == OrganizationUserStatusType.Confirmed &&
                x.Value.PlanType == PlanType.Free))
        {
            throw new BadRequestException(
                "User is an owner/admin of another free organization. Please have them upgrade to a paid plan to restore their account.");
        }
    }

    public async Task<List<Tuple<OrganizationUser, string>>> RestoreUsersAsync(Guid organizationId,
        IEnumerable<Guid> organizationUserIds, Guid? restoringUserId, IUserService userService)
    {
        var orgUsers = await organizationUserRepository.GetManyAsync(organizationUserIds);
        var filteredUsers = orgUsers.Where(u => u.OrganizationId == organizationId)
            .ToList();

        if (filteredUsers.Count == 0)
        {
            throw new BadRequestException("Users invalid.");
        }

        var organization = await organizationRepository.GetByIdAsync(organizationId);
        var seatCounts = await organizationRepository.GetOccupiedSeatCountByOrganizationIdAsync(organization.Id);
        var availableSeats = organization.Seats.GetValueOrDefault(0) - seatCounts.Total;
        var newSeatsRequired = organizationUserIds.Count() - availableSeats;
        await organizationService.AutoAddSeatsAsync(organization, newSeatsRequired);

        var deletingUserIsOwner = false;
        if (restoringUserId.HasValue)
        {
            deletingUserIsOwner = await currentContext.OrganizationOwner(organizationId);
        }

        // Query Two Factor Authentication status for all users in the organization
        // This is an optimization to avoid querying the Two Factor Authentication status for each user individually
        var organizationUsersTwoFactorEnabled = await twoFactorIsEnabledQuery.TwoFactorIsEnabledAsync(
            filteredUsers.Where(ou => ou.UserId.HasValue).Select(ou => ou.UserId.Value));

        var orgUsersAndOrgs = await GetRelatedOrganizationUsersAndOrganizationsAsync(filteredUsers);

        var result = new List<Tuple<OrganizationUser, string>>();

        foreach (var organizationUser in filteredUsers)
        {
            try
            {
                if (organizationUser.Status != OrganizationUserStatusType.Revoked)
                {
                    throw new BadRequestException("Already active.");
                }

                if (restoringUserId.HasValue && organizationUser.UserId == restoringUserId)
                {
                    throw new BadRequestException("You cannot restore yourself.");
                }

                if (organizationUser.Type == OrganizationUserType.Owner && restoringUserId.HasValue &&
                    !deletingUserIsOwner)
                {
                    throw new BadRequestException("Only owners can restore other owners.");
                }

                var twoFactorIsEnabled = organizationUser.UserId.HasValue
                                         && organizationUsersTwoFactorEnabled
                                             .FirstOrDefault(ou => ou.userId == organizationUser.UserId.Value)
                                             .twoFactorIsEnabled;

                await CheckPoliciesBeforeRestoreAsync(organizationUser, twoFactorIsEnabled);

                if (organization.PlanType == PlanType.Free)
                {
                    CheckForOtherFreeOrganizationOwnership(organizationUser, orgUsersAndOrgs);
                }

                var status = OrganizationService.GetPriorActiveOrganizationUserStatusType(organizationUser);

                await organizationUserRepository.RestoreAsync(organizationUser.Id, status);
                organizationUser.Status = status;
                await eventService.LogOrganizationUserEventAsync(organizationUser, EventType.OrganizationUser_Restored);
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

    private async Task CheckPoliciesBeforeRestoreAsync(OrganizationUser orgUser, bool userHasTwoFactorEnabled)
    {
        // An invited OrganizationUser isn't linked with a user account yet, so these checks are irrelevant
        // The user will be subject to the same checks when they try to accept the invite
        if (OrganizationService.GetPriorActiveOrganizationUserStatusType(orgUser) == OrganizationUserStatusType.Invited)
        {
            return;
        }

        var userId = orgUser.UserId.Value;

        // Enforce Single Organization Policy of organization user is being restored to
        var allOrgUsers = await organizationUserRepository.GetManyByUserAsync(userId);
        var hasOtherOrgs = allOrgUsers.Any(ou => ou.OrganizationId != orgUser.OrganizationId);
        var singleOrgPoliciesApplyingToRevokedUsers = await policyService.GetPoliciesApplicableToUserAsync(userId,
            PolicyType.SingleOrg, OrganizationUserStatusType.Revoked);
        var singleOrgPolicyApplies =
            singleOrgPoliciesApplyingToRevokedUsers.Any(p => p.OrganizationId == orgUser.OrganizationId);

        var singleOrgCompliant = true;
        var belongsToOtherOrgCompliant = true;
        var twoFactorCompliant = true;

        if (hasOtherOrgs && singleOrgPolicyApplies)
        {
            singleOrgCompliant = false;
        }

        // Enforce Single Organization Policy of other organizations user is a member of
        var anySingleOrgPolicies = await policyService.AnyPoliciesApplicableToUserAsync(userId, PolicyType.SingleOrg);
        if (anySingleOrgPolicies)
        {
            belongsToOtherOrgCompliant = false;
        }

        // Enforce 2FA Policy of organization user is trying to join
        if (!userHasTwoFactorEnabled)
        {
            twoFactorCompliant = !await IsTwoFactorRequiredForOrganizationAsync(userId, orgUser.OrganizationId);
        }

        var user = await userRepository.GetByIdAsync(userId);

        if (!singleOrgCompliant && !twoFactorCompliant)
        {
            throw new BadRequestException(user.Email +
                                          " is not compliant with the single organization and two-step login policy");
        }
        else if (!singleOrgCompliant)
        {
            throw new BadRequestException(user.Email + " is not compliant with the single organization policy");
        }
        else if (!belongsToOtherOrgCompliant)
        {
            throw new BadRequestException(user.Email +
                                          " belongs to an organization that doesn't allow them to join multiple organizations");
        }
        else if (!twoFactorCompliant)
        {
            throw new BadRequestException(user.Email + " is not compliant with the two-step login policy");
        }
    }

    private async Task<bool> IsTwoFactorRequiredForOrganizationAsync(Guid userId, Guid organizationId)
    {
        if (featureService.IsEnabled(FeatureFlagKeys.PolicyRequirements))
        {
            var requirement = await policyRequirementQuery.GetAsync<RequireTwoFactorPolicyRequirement>(userId);
            return requirement.IsTwoFactorRequiredForOrganization(organizationId);
        }

        var invitedTwoFactorPolicies = await policyService.GetPoliciesApplicableToUserAsync(userId,
            PolicyType.TwoFactorAuthentication, OrganizationUserStatusType.Revoked);
        return invitedTwoFactorPolicies.Any(p => p.OrganizationId == organizationId);
    }
}
