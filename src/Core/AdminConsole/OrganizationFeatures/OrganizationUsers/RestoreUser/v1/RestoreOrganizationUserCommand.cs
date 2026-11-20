// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.AutoConfirmUser;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Enforcement.AutoConfirm;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements.Errors;
using Bit.Core.AdminConsole.Utilities.v2;
using Bit.Core.Auth.UserFeatures.EmergencyAccess.Interfaces;
using Bit.Core.Auth.UserFeatures.TwoFactorAuth.Interfaces;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Organizations;
using Bit.Core.Billing.Organizations.Commands;
using Bit.Core.Billing.Organizations.Helpers;
using Bit.Core.Billing.Organizations.Models;
using Bit.Core.Billing.Organizations.Queries;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Microsoft.Extensions.Logging;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.RestoreUser.v1;

public class RestoreOrganizationUserCommand(
    ICurrentContext currentContext,
    IEventService eventService,
    IPushNotificationService pushNotificationService,
    IOrganizationUserRepository organizationUserRepository,
    IOrganizationRepository organizationRepository,
    ITwoFactorIsEnabledQuery twoFactorIsEnabledQuery,
    IUserRepository userRepository,
    IOrganizationService organizationService,
    IPolicyRequirementQuery policyRequirementQuery,
    ICollectionRepository collectionRepository,
    IAutomaticUserConfirmationPolicyEnforcementHandler automaticUserConfirmationPolicyEnforcementHandler,
    IDeleteEmergencyAccessCommand deleteEmergencyAccessCommand,
    TimeProvider timeProvider,
    IReservePrivilegedControlsSeatsCommand reservePrivilegedControlsSeatsCommand,
    ISendPrivilegedControlsSeatNeededEmailCommand sendPrivilegedControlsSeatNeededEmailCommand,
    ICountNewPamSeatsRequiredQuery countNewPamSeatsRequiredQuery,
    IGlobalSettings globalSettings,
    ILogger<RestoreOrganizationUserCommand> logger) : IRestoreOrganizationUserCommand
{
    public async Task RestoreUserAsync(OrganizationUser organizationUser, Guid? restoringUserId, string defaultCollectionName)
    {
        if (restoringUserId.HasValue && organizationUser.UserId == restoringUserId.Value)
        {
            throw new BadRequestException(new CannotRestoreYourself().Message);
        }

        if (organizationUser.Type == OrganizationUserType.Owner && restoringUserId.HasValue &&
            !await currentContext.OrganizationOwner(organizationUser.OrganizationId))
        {
            throw new BadRequestException(new OnlyOwnersCanRestoreOwners().Message);
        }

        if (organizationUser.Type == OrganizationUserType.Admin && restoringUserId.HasValue &&
            !await currentContext.OrganizationAdmin(organizationUser.OrganizationId))
        {
            throw new BadRequestException(new CustomUsersCannotRestoreAdmins().Message);
        }

        await RepositoryRestoreUserAsync(organizationUser, defaultCollectionName, null);
        await eventService.LogOrganizationUserEventAsync(organizationUser, EventType.OrganizationUser_Restored);

        if (organizationUser.UserId.HasValue)
        {
            await pushNotificationService.PushSyncOrgKeysAsync(organizationUser.UserId.Value);
        }
    }

    public async Task RestoreUserAsync(OrganizationUser organizationUser, EventSystemUser systemUser)
    {
        await RepositoryRestoreUserAsync(organizationUser, null, systemUser); // users stored by a system user will not get a default collection at this point.
        await eventService.LogOrganizationUserEventAsync(organizationUser, EventType.OrganizationUser_Restored,
            systemUser);

        if (organizationUser.UserId.HasValue)
        {
            await pushNotificationService.PushSyncOrgKeysAsync(organizationUser.UserId.Value);
        }
    }

    private async Task RepositoryRestoreUserAsync(OrganizationUser organizationUser, string defaultCollectionName,
        EventSystemUser? systemUser)
    {
        if (organizationUser.Status != OrganizationUserStatusType.Revoked)
        {
            throw new BadRequestException(new AlreadyActive().Message);
        }

        var organization = await organizationRepository.GetByIdAsync(organizationUser.OrganizationId);
        var status = organizationUser.GetPriorActiveOrganizationUserStatusType();
        var restoresPrivilegedControlsAccess = organization.UsePam && organizationUser.AccessPam &&
                                               PrivilegedControlsSeatOccupancy.OccupiesSeat(status);

        // Public API restores never buy Privileged Controls seats; they fail at the limit before any seat is bought.
        if (restoresPrivilegedControlsAccess && systemUser == EventSystemUser.PublicApi &&
            await countNewPamSeatsRequiredQuery.CountNewPamSeatsRequiredAsync(organization, 1) > 0)
        {
            throw new BadRequestException(globalSettings.SelfHosted
                ? new CannotAutoscalePrivilegedControlsSeatsOnSelfHostError().Message
                : new NotEnoughPrivilegedControlsSeatsError().Message);
        }

        var seatCounts = await organizationRepository.GetOccupiedSeatCountByOrganizationIdAsync(organization.Id);
        var availableSeats = organization.Seats.GetValueOrDefault(0) - seatCounts.Total;
        var initialPasswordManagerSeats = organization.Seats;

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

        if (organization.PlanType == PlanType.Free && organizationUser.UserId.HasValue)
        {
            await CheckUserForOtherFreeOrganizationOwnershipAsync(organizationUser);
        }

        await CheckPoliciesBeforeRestoreAsync(organizationUser, userTwoFactorIsEnabled);

        PrivilegedControlsSeatReservationResult scimPrivilegedControlsSeatReservation = null;

        if (restoresPrivilegedControlsAccess)
        {
            // No system user means an admin is restoring this one member, so one seat is reserved.
            if (systemUser is null)
            {
                await ReservePrivilegedControlsSeatsOrThrowAsync(organization, 1, initialPasswordManagerSeats);
            }
            // A SCIM restore with no free Privileged Controls seat must still go through: the member is restored
            // without access and the Owners are emailed.
            else if (systemUser == EventSystemUser.SCIM)
            {
                scimPrivilegedControlsSeatReservation = await reservePrivilegedControlsSeatsCommand.RunAsync(organization, 1);
                if (!scimPrivilegedControlsSeatReservation.Success)
                {
                    organizationUser.AccessPam = false;
                    // RestoreAsync only writes the status, so the removed access has to be saved separately.
                    await organizationUserRepository.ReplaceAsync(organizationUser);
                }
            }
        }

        await organizationUserRepository.RestoreAsync(organizationUser.Id, status);

        if (status == OrganizationUserStatusType.Invited)
        {
            await organizationUserRepository.StartInviteReminderSequencesAsync(
                [organizationUser.Id], timeProvider.GetUtcNow().UtcDateTime);
        }

        if (scimPrivilegedControlsSeatReservation?.Error is PrivilegedControlsSeatLimitReachedError)
        {
            var memberEmail = organizationUser.Email
                              ?? (await userRepository.GetByIdAsync(organizationUser.UserId!.Value)).Email;
            await sendPrivilegedControlsSeatNeededEmailCommand.RunAsync(organization, memberEmail);
        }

        if (organizationUser.UserId.HasValue
           && organization.UseMyItems
           && (await policyRequirementQuery.GetAsync<OrganizationDataOwnershipPolicyRequirement>(organizationUser.UserId.Value)).State == OrganizationDataOwnershipState.Enabled
           && status == OrganizationUserStatusType.Confirmed
           && !string.IsNullOrWhiteSpace(defaultCollectionName))
        {
            await collectionRepository.CreateDefaultCollectionsAsync(organizationUser.OrganizationId,
                [organizationUser.Id],
                defaultCollectionName);
        }
    }

    /// <summary>
    /// Reserves Privileged Controls seats for members an admin is restoring with access, through single or bulk
    /// restore. When the seats cannot be reserved, the Password Manager seats bought for the same restore are handed
    /// back and the restore fails.
    /// </summary>
    /// <param name="initialPasswordManagerSeats">The Password Manager seat count captured before the restore bought seats.</param>
    private async Task ReservePrivilegedControlsSeatsOrThrowAsync(Organization organization, int seatsRequested,
        int? initialPasswordManagerSeats)
    {
        var reservation = await reservePrivilegedControlsSeatsCommand.RunAsync(organization, seatsRequested);
        if (reservation.Success)
        {
            return;
        }

        await ReleasePasswordManagerSeatsAsync(organization, initialPasswordManagerSeats);
        throw reservation.Error is InternalError
            ? new InvalidOperationException(reservation.Error.Message, reservation.Exception)
            : new BadRequestException(reservation.Error!.Message);
    }

    /// <summary>
    /// Restores the Password Manager seat count captured before the restore bought seats.
    /// </summary>
    private async Task ReleasePasswordManagerSeatsAsync(Organization organization, int? initialPasswordManagerSeats)
    {
        if (initialPasswordManagerSeats is null || initialPasswordManagerSeats == organization.Seats)
        {
            return;
        }

        var seatsToRelease = organization.Seats.GetValueOrDefault() - initialPasswordManagerSeats.Value;

        try
        {
            await organizationService.AdjustSeatsAsync(organization.Id, -seatsToRelease);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failed to release {SeatsToRelease} auto-added seat(s) for organization {OrganizationId}",
                seatsToRelease, organization.Id);
        }
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
                new UserCannotBeRestoredFreeOrgAdminLimit().Message);
        }
    }

    public async Task<List<Tuple<OrganizationUser, string>>> RestoreUsersAsync(Guid organizationId,
        IEnumerable<Guid> organizationUserIds, Guid? restoringUserId, IUserService userService,
        string defaultCollectionName)
    {
        var orgUsers = await organizationUserRepository.GetManyAsync(organizationUserIds);
        var filteredUsers = orgUsers.Where(u => u.OrganizationId == organizationId)
            .ToList();

        if (filteredUsers.Count == 0)
        {
            throw new BadRequestException(new UsersInvalid().Message);
        }

        var organization = await organizationRepository.GetByIdAsync(organizationId);
        var seatCounts = await organizationRepository.GetOccupiedSeatCountByOrganizationIdAsync(organization.Id);
        var availableSeats = organization.Seats.GetValueOrDefault(0) - seatCounts.Total;
        var newSeatsRequired = organizationUserIds.Count() - availableSeats;
        var initialPasswordManagerSeats = organization.Seats;
        await organizationService.AutoAddSeatsAsync(organization, newSeatsRequired);

        if (organization.UsePam)
        {
            // Only revoked members are restored, and only those returning to a seat-using status with Privileged
            // Controls access need a seat. Seats are reserved once for the whole selection before the per-member
            // checks, the same way Password Manager seats are.
            var privilegedControlsSeatsRequested = filteredUsers.Count(u =>
                u.Status == OrganizationUserStatusType.Revoked
                && u.AccessPam
                && PrivilegedControlsSeatOccupancy.OccupiesSeat(u.GetPriorActiveOrganizationUserStatusType()));
            await ReservePrivilegedControlsSeatsOrThrowAsync(organization, privilegedControlsSeatsRequested,
                initialPasswordManagerSeats);
        }

        var restoringUserIsOwner = false;
        var restoringUserIsAdminOrHigher = false;
        if (restoringUserId.HasValue)
        {
            restoringUserIsOwner = await currentContext.OrganizationOwner(organizationId);
            restoringUserIsAdminOrHigher = await currentContext.OrganizationAdmin(organizationId);
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
                    throw new BadRequestException(new AlreadyActive().Message);
                }

                if (restoringUserId.HasValue && organizationUser.UserId == restoringUserId)
                {
                    throw new BadRequestException(new CannotRestoreYourself().Message);
                }

                if (organizationUser.Type == OrganizationUserType.Owner && restoringUserId.HasValue &&
                    !restoringUserIsOwner)
                {
                    throw new BadRequestException(new OnlyOwnersCanRestoreOwners().Message);
                }

                if (organizationUser.Type == OrganizationUserType.Admin && restoringUserId.HasValue &&
                    !restoringUserIsAdminOrHigher)
                {
                    throw new BadRequestException(new CustomUsersCannotRestoreAdmins().Message);
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

                var status = organizationUser.GetPriorActiveOrganizationUserStatusType();

                await organizationUserRepository.RestoreAsync(organizationUser.Id, status);
                organizationUser.Status = status;

                if (organizationUser.UserId.HasValue)
                {
                    await pushNotificationService.PushSyncOrgKeysAsync(organizationUser.UserId.Value);
                }

                await eventService.LogOrganizationUserEventAsync(organizationUser, EventType.OrganizationUser_Restored);

                result.Add(Tuple.Create(organizationUser, ""));
            }
            catch (BadRequestException e)
            {
                result.Add(Tuple.Create(organizationUser, e.Message));
            }
        }

        await CreateDefaultCollectionsForConfirmedUsersAsync(organization, defaultCollectionName,
            result.Where(r => r.Item2 == "").Select(x => x.Item1).ToList());

        var restoredInvitedUserIds = result
            .Where(r => r.Item2 == "" && r.Item1.Status == OrganizationUserStatusType.Invited)
            .Select(r => r.Item1.Id)
            .ToList();
        if (restoredInvitedUserIds.Count > 0)
        {
            await organizationUserRepository.StartInviteReminderSequencesAsync(
                restoredInvitedUserIds, timeProvider.GetUtcNow().UtcDateTime);
        }

        return result;
    }

    private async Task CreateDefaultCollectionsForConfirmedUsersAsync(Organization organization, string defaultCollectionName,
        ICollection<OrganizationUser> restoredUsers)
    {
        if (string.IsNullOrWhiteSpace(defaultCollectionName))
        {
            return;
        }

        if (!organization.UseMyItems)
        {
            return;
        }

        var restoredConfirmedUsers = restoredUsers
            .Where(w => w.Status == OrganizationUserStatusType.Confirmed)
            .Where(w => w.UserId != null)
            .Select(s => s.UserId.Value)
            .ToList();

        if (restoredConfirmedUsers.Count == 0)
        {
            return;
        }

        var restoredUserPolicyRequirements = await
            policyRequirementQuery.GetAsync<OrganizationDataOwnershipPolicyRequirement>(restoredConfirmedUsers);

        var orgUserIdsToCreateDefaultCollectionsFor = restoredUserPolicyRequirements
            .Select(s => s.Requirement.GetDefaultCollectionRequestOnConfirm(organization.Id))
            .Where(w => w.ShouldCreateDefaultCollection)
            .Select(s => s.OrganizationUserId)
            .ToList();

        if (orgUserIdsToCreateDefaultCollectionsFor.Count != 0)
        {
            await collectionRepository.CreateDefaultCollectionsAsync(organization.Id,
                orgUserIdsToCreateDefaultCollectionsFor,
                defaultCollectionName);
        }
    }

    private async Task CheckPoliciesBeforeRestoreAsync(OrganizationUser orgUser, bool userHasTwoFactorEnabled)
    {
        // An Invited or Staged OrganizationUser isn't linked with a user account yet, so these checks
        // are irrelevant (and there is no UserId to check against). The user will be subject to the
        // same checks when they accept the invite.
        var priorStatus = orgUser.GetPriorActiveOrganizationUserStatusType();
        if (priorStatus is OrganizationUserStatusType.Invited or OrganizationUserStatusType.Staged)
        {
            return;
        }

        var userId = orgUser.UserId.Value;

        var allOrgUsers = await organizationUserRepository.GetManyByUserAsync(userId);
        var user = await userRepository.GetByIdAsync(userId);

        var singleOrgRequirement = await policyRequirementQuery.GetAsync<SingleOrganizationPolicyRequirement>(userId);
        var singleOrgError = singleOrgRequirement.CanJoinOrganization(orgUser.OrganizationId, allOrgUsers);

        var twoFactorCompliant = userHasTwoFactorEnabled || !await IsTwoFactorRequiredForOrganizationAsync(userId, orgUser.OrganizationId);

        if (singleOrgError is not null && !twoFactorCompliant)
        {
            throw new BadRequestException(new UserCannotBeRestoredNotCompliantWithPolicies(user.Email).Message);
        }

        if (singleOrgError is not null)
        {
            var singleOrgErrorMessage = singleOrgError switch
            {
                UserIsAMemberOfAnotherOrganization => new UserCannotBeRestoredMemberOfAnotherOrg(user.Email).Message,
                UserIsAMemberOfAnOrganizationThatHasSingleOrgPolicy => new UserCannotBeRestoredForbiddenByOtherOrg(user.Email).Message,
                _ => singleOrgError.Message
            };

            throw new BadRequestException(singleOrgErrorMessage);
        }

        if (!twoFactorCompliant)
        {
            throw new BadRequestException(new UserNotCompliantWithTwoFactorPolicy(user.Email).Message);
        }

        var policyRequirement = await policyRequirementQuery.GetAsync<AutomaticUserConfirmationPolicyRequirement>(
            user.Id);

        var validationResult = await automaticUserConfirmationPolicyEnforcementHandler.IsCompliantAsync(
            new AutomaticUserConfirmationPolicyEnforcementRequest(orgUser.OrganizationId, allOrgUsers, user!),
            policyRequirement);

        var badRequestException = validationResult.Match(
            error =>
            {
                var message = error switch
                {
                    UserCannotBelongToAnotherOrganization => new UserCannotBeRestoredMemberOfAnotherOrg(user.Email).Message,
                    OtherOrganizationDoesNotAllowOtherMembership => new UserCannotBeRestoredForbiddenByOtherOrg(user.Email).Message,
                    _ => error.Message
                };
                return new BadRequestException(message);
            },
            _ => null);

        if (badRequestException is not null)
        {
            throw badRequestException;
        }

        if (policyRequirement.IsEnabled(orgUser.OrganizationId))
        {
            await deleteEmergencyAccessCommand.DeleteAllByUserIdAsync(user.Id);
        }
    }

    private async Task<bool> IsTwoFactorRequiredForOrganizationAsync(Guid userId, Guid organizationId)
    {
        var requirement = await policyRequirementQuery.GetAsync<RequireTwoFactorPolicyRequirement>(userId);
        return requirement.IsTwoFactorRequiredForOrganization(organizationId);
    }
}
