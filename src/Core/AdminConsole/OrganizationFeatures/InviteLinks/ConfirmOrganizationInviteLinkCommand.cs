using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks.Interfaces;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.UpdateUserResetPasswordEnrollment;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Utilities;
using Bit.Core.AdminConsole.Utilities.v2;
using Bit.Core.AdminConsole.Utilities.v2.Results;
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
/// This command looks up the invite link, organization, and existing membership, and delegates eligibility to
/// <see cref="IConfirmOrganizationInviteLinkValidator"/>, which performs the read-only prechecks. This command
/// owns the write side effects: creating the membership when needed, confirming it with the organization key, and
/// running the policy-driven follow-ups.
/// </remarks>
public class ConfirmOrganizationInviteLinkCommand(
    IConfirmOrganizationInviteLinkValidator confirmOrganizationInviteLinkValidator,
    IOrganizationInviteLinkRepository organizationInviteLinkRepository,
    IOrganizationRepository organizationRepository,
    IOrganizationUserRepository organizationUserRepository,
    ICollectionRepository collectionRepository,
    IPolicyRequirementQuery policyRequirementQuery,
    IOrganizationService organizationService,
    IStripePaymentService stripePaymentService,
    IUpdateUserResetPasswordEnrollmentCommand updateUserResetPasswordEnrollmentCommand,
    IPushNotificationService pushNotificationService,
    IEventService eventService,
    ILogger<ConfirmOrganizationInviteLinkCommand> logger)
    : IConfirmOrganizationInviteLinkCommand
{
    public async Task<CommandResult> ConfirmAsync(ConfirmOrganizationInviteLinkRequest request)
    {
        var validationRequest = await BuildValidationRequestAsync(request);
        var validationResult = await confirmOrganizationInviteLinkValidator.ValidateAsync(validationRequest);
        if (validationResult.IsError)
        {
            return validationResult.AsError;
        }

        // Validation guarantees the organization exists.
        var organization = validationRequest.Organization!;

        // Account recovery enrollment is validated before any writes so a missing key fails the request
        // without leaving a partially confirmed membership behind.
        var autoEnrollEnabled = await IsAccountRecoveryAutoEnrollEnabledAsync(request.User, organization);
        if (autoEnrollEnabled && !OrganizationUser.IsValidResetPasswordKey(request.ResetPasswordKey))
        {
            return new ConfirmResetPasswordKeyRequired();
        }

        var membershipResult = await AddUserToOrganizationAsync(
            request, validationRequest.ExistingOrganizationUser, request.User, organization);
        if (membershipResult.IsError)
        {
            return membershipResult.AsError;
        }

        await PerformPostConfirmSideEffectsAsync(request, organization, membershipResult.AsSuccess, autoEnrollEnabled);

        return new None();
    }

    /// <summary>
    /// Looks up the invite link, its organization, and the user's existing membership for the validator.
    /// </summary>
    private async Task<OrganizationInviteLinkValidationRequest> BuildValidationRequestAsync(
        ConfirmOrganizationInviteLinkRequest request)
    {
        var inviteLink = await organizationInviteLinkRepository.GetByOrganizationIdAsync(request.OrganizationId);
        var organization = inviteLink is null
            ? null
            : await organizationRepository.GetByIdAsync(request.OrganizationId);
        var existingOrganizationUser = organization is null
            ? null
            : await ResolveExistingOrganizationUserAsync(organization, request.User);

        return new OrganizationInviteLinkValidationRequest
        {
            InviteLink = inviteLink,
            Code = request.Code,
            Organization = organization,
            User = request.User,
            ExistingOrganizationUser = existingOrganizationUser,
        };
    }

    private async Task<bool> IsAccountRecoveryAutoEnrollEnabledAsync(User user, Organization organization)
    {
        var resetPasswordRequirement = await policyRequirementQuery.GetAsync<ResetPasswordPolicyRequirement>(user.Id);
        return resetPasswordRequirement.AutoEnrollEnabled(organization.Id);
    }

    /// <summary>
    /// The follow-ups once the user is confirmed: the audit event, the default collection, account recovery
    /// enrollment, and syncing the organization key to the user's other devices.
    /// </summary>
    private async Task PerformPostConfirmSideEffectsAsync(ConfirmOrganizationInviteLinkRequest request,
        Organization organization, OrganizationUser organizationUser, bool autoEnrollEnabled)
    {
        var user = request.User;

        await eventService.LogOrganizationUserEventAsync(organizationUser, EventType.OrganizationUser_InviteLinkConfirmed);

        await CreateDefaultCollectionAsync(organization, organizationUser, request.DefaultUserCollectionName);

        if (autoEnrollEnabled)
        {
            await updateUserResetPasswordEnrollmentCommand.UpdateUserResetPasswordEnrollmentAsync(
                organization.Id, user.Id, request.ResetPasswordKey, user.Id);
        }

        // The membership now carries the organization key, so notify the user's other devices to sync it.
        await pushNotificationService.PushSyncOrgKeysAsync(user.Id);
    }

    /// <summary>
    /// Resolves the membership to confirm, preferring an existing user-linked membership and falling
    /// back to a pending email invitation for the same address.
    /// </summary>
    private async Task<OrganizationUser?> ResolveExistingOrganizationUserAsync(Organization organization, User user)
    {
        var userLinkedOrganizationUser = await organizationUserRepository.GetByOrganizationAsync(organization.Id, user.Id);
        if (userLinkedOrganizationUser is not null)
        {
            return userLinkedOrganizationUser;
        }

        return await organizationUserRepository.GetByOrganizationEmailAsync(organization.Id, user.Email);
    }

    private async Task<CommandResult<OrganizationUser>> AddUserToOrganizationAsync(ConfirmOrganizationInviteLinkRequest request,
        OrganizationUser? existingOrganizationUser, User user, Organization organization)
    {
        if (existingOrganizationUser is not null)
        {
            return await ConfirmExistingMembershipAsync(organization, existingOrganizationUser, user, request.OrgUserKey);
        }

        return await CreateConfirmedMembershipAsync(organization, user, request.OrgUserKey);
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
    /// leaves no orphaned seat. The validator has already confirmed the plan permits an additional seat.
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
    /// Reserves capacity for one more seat-occupying member. Runs before the membership is written so that a
    /// billing or persistence failure leaves no orphaned seat. The seat count is re-read here because it may
    /// have changed since the validator ran.
    /// </summary>
    private async Task<Error?> ReserveSeatAsync(Organization organization)
    {
        var occupiedSeatCount = (await organizationRepository.GetOccupiedSeatCountByOrganizationIdAsync(organization.Id)).Total;
        if (!OrganizationSeatAvailability.HasAvailableSeats(organization, occupiedSeatCount))
        {
            return new ConfirmOrganizationHasNoAvailableSeats(organization.DisplayName());
        }

        return await TryExpandSeatsAsync(organization, occupiedSeatCount);
    }

    /// <summary>
    /// Only auto-adds when the organization is already at capacity.
    /// </summary>
    private async Task<Error?> TryExpandSeatsAsync(Organization organization, int occupiedSeatCount)
    {
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
