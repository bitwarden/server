using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks.Interfaces;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.UpdateUserResetPasswordEnrollment;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.AdminConsole.Utilities.v2;
using Bit.Core.AdminConsole.Utilities.v2.Results;
using Bit.Core.Auth.UserFeatures.EmergencyAccess.Interfaces;
using Bit.Core.Billing.Services;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Microsoft.Extensions.Logging;
using None = OneOf.Types.None;

namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;

/// <summary>
/// Confirms a user into an organization via an invite link. See
/// <see cref="IConfirmOrganizationInviteLinkCommand"/> for the behavior.
/// </summary>
/// <remarks>
/// Eligibility is delegated to <see cref="IConfirmOrganizationInviteLinkValidator"/>, which performs the
/// read-only prechecks, including all policy checks. This command owns the writes: creating the membership when
/// needed, confirming it with the organization key, and running the post-confirm side effects.
/// </remarks>
public class ConfirmOrganizationInviteLinkCommand(
    IConfirmOrganizationInviteLinkValidator confirmOrganizationInviteLinkValidator,
    IOrganizationRepository organizationRepository,
    IOrganizationUserRepository organizationUserRepository,
    ICollectionRepository collectionRepository,
    IPolicyRequirementQuery policyRequirementQuery,
    IOrganizationService organizationService,
    IStripePaymentService stripePaymentService,
    IUpdateUserResetPasswordEnrollmentCommand updateUserResetPasswordEnrollmentCommand,
    IDeleteEmergencyAccessCommand deleteEmergencyAccessCommand,
    IPushNotificationService pushNotificationService,
    IEventService eventService,
    ILogger<ConfirmOrganizationInviteLinkCommand> logger)
    : IConfirmOrganizationInviteLinkCommand
{
    public async Task<CommandResult> ConfirmAsync(ConfirmOrganizationInviteLinkRequest request)
    {
        var validationResult = await confirmOrganizationInviteLinkValidator.ValidateAsync(
            new ConfirmOrganizationInviteLinkValidationRequest
            {
                OrganizationId = request.OrganizationId,
                Code = request.Code,
                User = request.User,
                ResetPasswordKey = request.ResetPasswordKey,
            });
        if (validationResult.IsError)
        {
            return validationResult.AsError;
        }

        var validated = validationResult.AsSuccess;

        var membershipResult = await AddUserToOrganizationAsync(request, validated);
        if (membershipResult.IsError)
        {
            return membershipResult.AsError;
        }

        await PerformPostConfirmSideEffectsAsync(request, validated, membershipResult.AsSuccess);

        return new None();
    }

    private async Task<CommandResult<OrganizationUser>> AddUserToOrganizationAsync(
        ConfirmOrganizationInviteLinkRequest request, ConfirmOrganizationInviteLinkValidationResult validated)
    {
        if (validated.ExistingOrganizationUser is not null)
        {
            return await ConfirmExistingMembershipAsync(validated.Organization, validated.ExistingOrganizationUser,
                request.User, request.OrgUserKey);
        }

        return await CreateConfirmedMembershipAsync(validated.Organization, request.User, request.OrgUserKey);
    }

    private async Task PerformPostConfirmSideEffectsAsync(ConfirmOrganizationInviteLinkRequest request,
        ConfirmOrganizationInviteLinkValidationResult validated, OrganizationUser organizationUser)
    {
        var user = request.User;
        var organization = validated.Organization;

        await eventService.LogOrganizationUserEventAsync(organizationUser, EventType.OrganizationUser_InviteLinkConfirmed);

        await CreateDefaultCollectionAsync(organization, organizationUser, request.DefaultUserCollectionName);

        if (validated.AccountRecoveryAutoEnrollEnabled)
        {
            await updateUserResetPasswordEnrollmentCommand.UpdateUserResetPasswordEnrollmentAsync(
                organization.Id, user.Id, request.ResetPasswordKey, user.Id);
        }

        if (validated.AutoConfirmPolicyEnabled)
        {
            await deleteEmergencyAccessCommand.DeleteAllByUserIdAsync(user.Id);
        }

        // The membership now carries the organization key, so notify the user's other devices to sync it.
        await pushNotificationService.PushSyncOrgKeysAsync(user.Id);
    }

    /// <summary>
    /// Confirms an existing membership (a pending email invitation, a Staged provisioning row, or an accepted
    /// membership) by linking it to the user, releasing the org key, and moving it straight to
    /// <see cref="OrganizationUserStatusType.Confirmed"/>. A Staged row does not occupy a seat, so seats are
    /// expanded first when needed. Persisting via <c>ReplaceAsync</c> bumps the user's account revision date
    /// so their other devices sync.
    /// </summary>
    private async Task<CommandResult<OrganizationUser>> ConfirmExistingMembershipAsync(
        Organization organization, OrganizationUser existingOrganizationUser, User user, string orgUserKey)
    {
        if (existingOrganizationUser.Status == OrganizationUserStatusType.Staged)
        {
            var seatReservationError = await ReserveSeatAsync(organization);
            if (seatReservationError is not null)
            {
                return seatReservationError;
            }
        }

        existingOrganizationUser.Status = OrganizationUserStatusType.Confirmed;
        existingOrganizationUser.UserId = user.Id;
        existingOrganizationUser.Email = null;
        existingOrganizationUser.Key = orgUserKey;

        await organizationUserRepository.ReplaceAsync(existingOrganizationUser);

        return existingOrganizationUser;
    }

    /// <summary>
    /// Creates a new membership for the user directly in <see cref="OrganizationUserStatusType.Confirmed"/>
    /// status with the org key, expanding the organization's seats first so a billing or persistence failure
    /// leaves no orphaned seat.
    /// </summary>
    private async Task<CommandResult<OrganizationUser>> CreateConfirmedMembershipAsync(
        Organization organization, User user, string orgUserKey)
    {
        var seatReservationError = await ReserveSeatAsync(organization);
        if (seatReservationError is not null)
        {
            return seatReservationError;
        }

        var accessSecretsManager = await stripePaymentService.HasSecretsManagerStandalone(organization);
        var organizationUser = new OrganizationUser
        {
            OrganizationId = organization.Id,
            UserId = user.Id,
            Status = OrganizationUserStatusType.Confirmed,
            Type = OrganizationUserType.User,
            AccessSecretsManager = accessSecretsManager,
            Key = orgUserKey,
        };
        organizationUser.SetNewId();

        await organizationUserRepository.CreateAsync(organizationUser);

        return organizationUser;
    }

    /// <summary>
    /// Auto-adds a seat if the organization is at capacity. Runs before the membership is written so that a
    /// billing or persistence failure leaves no orphaned seat.
    /// </summary>
    private async Task<Error?> ReserveSeatAsync(Organization organization)
    {
        var occupiedSeatCount = (await organizationRepository.GetOccupiedSeatCountByOrganizationIdAsync(organization.Id)).Total;
        if (!organization.Seats.HasValue || occupiedSeatCount < organization.Seats.Value)
        {
            return null;
        }

        try
        {
            await organizationService.AutoAddSeatsAsync(organization, 1);
            return null;
        }
        catch (Exception ex) when (ex is BadRequestException or GatewayException)
        {
            // Known business failures (no payment method, autoscale cap, etc.) map to a 400.
            // Infrastructure failures propagate so they surface as 5xx with a correlation id.
            logger.LogWarning(ex, "Could not auto-add seat while confirming invite link for organization {OrganizationId}", organization.Id);
            return new ConfirmSeatAddFailed();
        }
    }

    /// <summary>
    /// Creates the user's default collection when the Organization Data Ownership policy applies. Failures
    /// are logged but not surfaced: the user is already confirmed and the collection can be recreated.
    /// </summary>
    private async Task CreateDefaultCollectionAsync(Organization organization, OrganizationUser organizationUser, string defaultUserCollectionName)
    {
        try
        {
            if (!await ShouldCreateDefaultCollectionAsync(organization, organizationUser, defaultUserCollectionName))
            {
                return;
            }

            await collectionRepository.CreateDefaultCollectionsAsync(
                organization.Id,
                [organizationUser.Id],
                defaultUserCollectionName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create default collection for user confirmed via invite link.");
        }
    }

    private async Task<bool> ShouldCreateDefaultCollectionAsync(Organization organization, OrganizationUser organizationUser, string defaultUserCollectionName) =>
        !string.IsNullOrWhiteSpace(defaultUserCollectionName)
        && organization.UseMyItems
        && (await policyRequirementQuery.GetAsync<OrganizationDataOwnershipPolicyRequirement>(organizationUser.UserId!.Value))
            .GetDefaultCollectionRequestOnConfirm(organization.Id).ShouldCreateDefaultCollection;
}
