using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Services;
using Bit.Core.Auth.UserFeatures.TwoFactorAuth.Interfaces;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;
using Bit.Core.Services;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.RestoreOrganizationUser;

public class RestoreOrganizationUserCommand(
    ICurrentContext currentContext,
    IEventService eventService,
    IPushNotificationService pushNotificationService,
    IFeatureService featureService,
    IOrganizationRepository organizationRepository,
    IOrganizationUserRepository organizationUserRepository,
    IOrganizationService organizationService,
    ITwoFactorIsEnabledQuery twoFactorIsEnabledQuery,
    IPolicyService policyService,
    IUserRepository userRepository) : IRestoreOrganizationUserCommand
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

        if (featureService.IsEnabled(FeatureFlagKeys.PushSyncOrgKeysOnRevokeRestore) && organizationUser.UserId.HasValue)
        {
            await pushNotificationService.PushSyncOrgKeysAsync(organizationUser.UserId.Value);
        }
    }

    public async Task RestoreUserAsync(OrganizationUser organizationUser, EventSystemUser systemUser)
    {
        await RepositoryRestoreUserAsync(organizationUser);
        await eventService.LogOrganizationUserEventAsync(organizationUser, EventType.OrganizationUser_Restored, systemUser);

        if (featureService.IsEnabled(FeatureFlagKeys.PushSyncOrgKeysOnRevokeRestore) && organizationUser.UserId.HasValue)
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
        var occupiedSeats = await organizationUserRepository.GetOccupiedSeatCountByOrganizationIdAsync(organization.Id);
        var availableSeats = organization.Seats.GetValueOrDefault(0) - occupiedSeats;
        if (availableSeats < 1)
        {
            await organizationService.AutoAddSeatsAsync(organization, 1);
        }

        var userTwoFactorIsEnabled = false;
        // Only check Two Factor Authentication status if the user is linked to a user account
        if (organizationUser.UserId.HasValue)
        {
            userTwoFactorIsEnabled = (await twoFactorIsEnabledQuery.TwoFactorIsEnabledAsync(new[] { organizationUser.UserId.Value })).FirstOrDefault().twoFactorIsEnabled;
        }

        await CheckPoliciesBeforeRestoreAsync(organizationUser, userTwoFactorIsEnabled);

        var status = GetPriorActiveOrganizationUserStatusType(organizationUser);

        await organizationUserRepository.RestoreAsync(organizationUser.Id, status);
        organizationUser.Status = status;
    }

    public async Task<List<Tuple<OrganizationUser, string>>> RestoreUsersAsync(Guid organizationId,
        IEnumerable<Guid> organizationUserIds, Guid? restoringUserId, IUserService userService)
    {
        var orgUsers = await organizationUserRepository.GetManyAsync(organizationUserIds);
        var filteredUsers = orgUsers.Where(u => u.OrganizationId == organizationId)
            .ToList();

        if (!filteredUsers.Any())
        {
            throw new BadRequestException("Users invalid.");
        }

        var organization = await organizationRepository.GetByIdAsync(organizationId);
        var occupiedSeats = await organizationUserRepository.GetOccupiedSeatCountByOrganizationIdAsync(organization.Id);
        var availableSeats = organization.Seats.GetValueOrDefault(0) - occupiedSeats;
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

                var status = GetPriorActiveOrganizationUserStatusType(organizationUser);

                await organizationUserRepository.RestoreAsync(organizationUser.Id, status);
                organizationUser.Status = status;
                await eventService.LogOrganizationUserEventAsync(organizationUser, EventType.OrganizationUser_Restored);
                if (featureService.IsEnabled(FeatureFlagKeys.PushSyncOrgKeysOnRevokeRestore) &&
                    organizationUser.UserId.HasValue)
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
        if (GetPriorActiveOrganizationUserStatusType(orgUser) == OrganizationUserStatusType.Invited)
        {
            return;
        }

        var userId = orgUser.UserId.Value;

        // Enforce Single Organization Policy of organization user is being restored to
        var allOrgUsers = await organizationUserRepository.GetManyByUserAsync(userId);
        var hasOtherOrgs = allOrgUsers.Any(ou => ou.OrganizationId != orgUser.OrganizationId);
        var singleOrgPoliciesApplyingToRevokedUsers = await policyService.GetPoliciesApplicableToUserAsync(userId,
            PolicyType.SingleOrg, OrganizationUserStatusType.Revoked);
        var singleOrgPolicyApplies = singleOrgPoliciesApplyingToRevokedUsers.Any(p => p.OrganizationId == orgUser.OrganizationId);

        var singleOrgCompliant = true;
        var belongsToOtherOrgCompliant = true;
        var twoFactorCompliant = true;

        if (hasOtherOrgs && singleOrgPolicyApplies)
        {
            singleOrgCompliant = false;
        }

        // Enforce Single Organization Policy of other organizations user is a member of
        var anySingleOrgPolicies = await policyService.AnyPoliciesApplicableToUserAsync(userId,
            PolicyType.SingleOrg);
        if (anySingleOrgPolicies)
        {
            belongsToOtherOrgCompliant = false;
        }

        // Enforce Two Factor Authentication Policy of organization user is trying to join
        if (!userHasTwoFactorEnabled)
        {
            var invitedTwoFactorPolicies = await policyService.GetPoliciesApplicableToUserAsync(userId,
                PolicyType.TwoFactorAuthentication, OrganizationUserStatusType.Revoked);
            if (invitedTwoFactorPolicies.Any(p => p.OrganizationId == orgUser.OrganizationId))
            {
                twoFactorCompliant = false;
            }
        }

        var user = await userRepository.GetByIdAsync(userId);

        if (!singleOrgCompliant && !twoFactorCompliant)
        {
            throw new BadRequestException(user.Email + " is not compliant with the single organization and two-step login polciy");
        }
        else if (!singleOrgCompliant)
        {
            throw new BadRequestException(user.Email + " is not compliant with the single organization policy");
        }
        else if (!belongsToOtherOrgCompliant)
        {
            throw new BadRequestException(user.Email + " belongs to an organization that doesn't allow them to join multiple organizations");
        }
        else if (!twoFactorCompliant)
        {
            throw new BadRequestException(user.Email + " is not compliant with the two-step login policy");
        }
    }

    static OrganizationUserStatusType GetPriorActiveOrganizationUserStatusType(OrganizationUser organizationUser)
    {
        // Determine status to revert back to
        var status = OrganizationUserStatusType.Invited;
        if (organizationUser.UserId.HasValue && string.IsNullOrWhiteSpace(organizationUser.Email))
        {
            // Has UserId & Email is null, then Accepted
            status = OrganizationUserStatusType.Accepted;
            if (!string.IsNullOrWhiteSpace(organizationUser.Key))
            {
                // We have an org key for this user, user was confirmed
                status = OrganizationUserStatusType.Confirmed;
            }
        }

        return status;
    }
}
