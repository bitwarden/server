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
/// read-only prechecks. This command owns the write side effects: creating the membership when needed,
/// confirming it with the organization key, and running the policy-driven follow-ups.
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
    ILogger<ConfirmOrganizationInviteLinkCommand> logger)
    : IConfirmOrganizationInviteLinkCommand
{
    public async Task<CommandResult> ConfirmAsync(ConfirmOrganizationInviteLinkRequest request)
    {
        var user = request.User;

        var validationResult = await confirmOrganizationInviteLinkValidator.ValidateAsync(
            new ConfirmOrganizationInviteLinkValidationRequest
            {
                Code = request.Code,
                User = user,
            });
        if (validationResult.IsError)
        {
            return validationResult.AsError;
        }

        var organization = validationResult.AsSuccess.Organization;
        var existingOrganizationUser = validationResult.AsSuccess.ExistingOrganizationUser;

        // Account recovery enrollment is validated before any writes so a missing key fails the request
        // without leaving a partially confirmed membership behind.
        var resetPasswordRequirement = await policyRequirementQuery.GetAsync<ResetPasswordPolicyRequirement>(user.Id);
        var autoEnrollEnabled = resetPasswordRequirement.AutoEnrollEnabled(organization.Id);
        if (autoEnrollEnabled && !OrganizationUser.IsValidResetPasswordKey(request.ResetPasswordKey))
        {
            return new ResetPasswordKeyRequired();
        }

        CommandResult<OrganizationUser> membershipResult;
        if (existingOrganizationUser is not null)
        {
            membershipResult = await ConfirmExistingMembershipAsync(existingOrganizationUser, user, request.OrgUserKey);
        }
        else
        {
            membershipResult = await CreateConfirmedMembershipAsync(organization, user, request.OrgUserKey);
        }
        if (membershipResult.IsError)
        {
            return membershipResult.AsError;
        }

        var organizationUser = membershipResult.AsSuccess;

        await CreateDefaultCollectionAsync(organization, organizationUser, request.DefaultUserCollectionName);

        if (autoEnrollEnabled)
        {
            await updateUserResetPasswordEnrollmentCommand.UpdateUserResetPasswordEnrollmentAsync(
                organization.Id, user.Id, request.ResetPasswordKey, user.Id);
        }

        // The user is now a confirmed member of an organization, so any emergency access they granted or
        // were granted is removed.
        await deleteEmergencyAccessCommand.DeleteAllByUserIdAsync(user.Id);

        return new None();
    }

    /// <summary>
    /// Confirms an existing membership (a pending email invitation or an accepted membership) by linking
    /// it to the user, releasing the org key, and moving it straight to <see cref="OrganizationUserStatusType.Confirmed"/>.
    /// Persisting via <c>ReplaceAsync</c> bumps the user's account revision date so their other devices sync.
    /// </summary>
    private async Task<CommandResult<OrganizationUser>> ConfirmExistingMembershipAsync(
        OrganizationUser existingOrganizationUser, User user, string orgUserKey)
    {
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
        var occupiedSeatCount = (await organizationRepository.GetOccupiedSeatCountByOrganizationIdAsync(organization.Id)).Total;

        var seatExpansionError = await TryExpandSeatsAsync(organization, occupiedSeatCount);
        if (seatExpansionError is not null)
        {
            return seatExpansionError;
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
    /// Expands the organization's seats before the new membership is created. Only auto-adds when the
    /// org is already at capacity.
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
            return new SeatAddFailed();
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
