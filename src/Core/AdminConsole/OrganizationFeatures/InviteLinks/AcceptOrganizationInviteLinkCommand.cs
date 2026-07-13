using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks.Interfaces;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.AcceptMembership;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.UpdateUserResetPasswordEnrollment;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Utilities;
using Bit.Core.AdminConsole.Utilities.v2;
using Bit.Core.AdminConsole.Utilities.v2.Results;
using Bit.Core.Auth.UserFeatures.EmergencyAccess.Interfaces;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Services;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Microsoft.Extensions.Logging;

namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;

public class AcceptOrganizationInviteLinkCommand(
    IOrganizationInviteLinkRepository organizationInviteLinkRepository,
    IOrganizationRepository organizationRepository,
    IOrganizationUserRepository organizationUserRepository,
    IProviderUserRepository providerUserRepository,
    IPolicyRequirementQuery policyRequirementQuery,
    IAcceptOrganizationMembershipValidator acceptOrganizationMembershipValidator,
    IOrganizationService organizationService,
    IStripePaymentService stripePaymentService,
    IUpdateUserResetPasswordEnrollmentCommand updateUserResetPasswordEnrollmentCommand,
    IMailService mailService,
    IPushAutoConfirmNotificationCommand pushAutoConfirmNotificationCommand,
    IDeleteEmergencyAccessCommand deleteEmergencyAccessCommand,
    ILogger<AcceptOrganizationInviteLinkCommand> logger,
    IEventService eventService)
    : IAcceptOrganizationInviteLinkCommand
{
    public async Task<CommandResult<OrganizationUser>> AcceptAsync(AcceptOrganizationInviteLinkRequest request)
    {
        var user = request.User;

        var link = await organizationInviteLinkRepository.GetByOrganizationIdAsync(request.OrganizationId);
        if (link is null || !InviteLinkCodeValidator.CodesMatch(request.Code.ToString(), link.Code))
        {
            return new InviteLinkNotFound();
        }

        var organization = await organizationRepository.GetByIdAsync(link.OrganizationId);
        if (organization is null or { Enabled: false })
        {
            return new InviteLinkNotFound();
        }

        if (!organization.UseInviteLinks)
        {
            return new InviteLinkNotAvailable();
        }

        if (!InviteLinkDomainValidator.IsEmailDomainAllowed(user.Email, link.GetAllowedDomains()))
        {
            return new EmailDomainNotAllowed();
        }

        // Provider users cannot accept invite links
        if ((await providerUserRepository.GetManyByUserAsync(user.Id)).Count != 0)
        {
            return new ProviderUsersCannotAcceptInviteLink();
        }

        var existingOrganizationUser = await ResolveExistingOrganizationUserAsync(organization, user);

        var membershipStatusError = ValidateExistingMembershipStatus(existingOrganizationUser);
        if (membershipStatusError is not null)
        {
            return membershipStatusError;
        }

        var allOrganizationMemberships = await organizationUserRepository.GetManyByUserAsync(user.Id);

        var membershipValidationRequest = new AcceptOrganizationMembershipValidationRequest
        {
            OrganizationId = organization.Id,
            User = user,
            AllOrganizationMemberships = allOrganizationMemberships,
            ExistingMembership = existingOrganizationUser,
        };
        var membershipValidationResult = await acceptOrganizationMembershipValidator
            .ValidateAsync(membershipValidationRequest);
        if (membershipValidationResult.IsError)
        {
            return membershipValidationResult.AsError;
        }

        var resetPasswordRequirement = await policyRequirementQuery.GetAsync<ResetPasswordPolicyRequirement>(user.Id);
        var autoEnrollEnabled = resetPasswordRequirement.AutoEnrollEnabled(organization.Id);
        if (autoEnrollEnabled && !OrganizationUser.IsValidResetPasswordKey(request.ResetPasswordKey))
        {
            return new ResetPasswordKeyRequired();
        }

        var autoConfirmPolicyEnabled = membershipValidationResult.Request.AutoConfirmPolicyEnabled;

        var acceptResult = existingOrganizationUser is not null
            ? await AcceptExistingInviteAsync(organization, existingOrganizationUser, user, autoConfirmPolicyEnabled)
            : await CreateNewMembershipAsync(organization, user, autoConfirmPolicyEnabled);
        if (acceptResult.IsError)
        {
            return acceptResult;
        }

        await eventService.LogOrganizationUserEventAsync(acceptResult.AsSuccess, EventType.OrganizationUser_InviteLinkAccepted);

        await PerformPostAcceptSideEffectsAsync(organization, user, autoEnrollEnabled, request.ResetPasswordKey, autoConfirmPolicyEnabled);

        return acceptResult;
    }

    /// <summary>
    /// Resolves the organization user to accept, preferring an existing user-linked membership
    /// and falling back to a pending email invitation for the same address.
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

    private static Error? ValidateExistingMembershipStatus(OrganizationUser? existingOrganizationUser) =>
        existingOrganizationUser?.Status switch
        {
            OrganizationUserStatusType.Revoked => new OrganizationAccessRevoked(),
            OrganizationUserStatusType.Accepted or OrganizationUserStatusType.Confirmed => new AlreadyOrganizationMember(),
            _ => null
        };

    private async Task<CommandResult<OrganizationUser>> AcceptExistingInviteAsync(
        Organization organization, OrganizationUser existingOrganizationUser, User user, bool autoConfirmPolicyEnabled)
    {
        var freeAdminError = await ValidateFreeOrganizationAdminLimitAsync(organization, existingOrganizationUser, user);
        if (freeAdminError is not null)
        {
            return freeAdminError;
        }

        existingOrganizationUser.Status = OrganizationUserStatusType.Accepted;
        existingOrganizationUser.UserId = user.Id;
        existingOrganizationUser.Email = null;

        if (autoConfirmPolicyEnabled)
        {
            await deleteEmergencyAccessCommand.DeleteAllByUserIdAsync(user.Id);
        }

        await organizationUserRepository.ReplaceAsync(existingOrganizationUser);

        return existingOrganizationUser;
    }

    private async Task<CommandResult<OrganizationUser>> CreateNewMembershipAsync(
        Organization organization, User user, bool autoConfirmPolicyEnabled)
    {
        var occupiedSeatCount = (await organizationRepository.GetOccupiedSeatCountByOrganizationIdAsync(organization.Id)).Total;
        if (!OrganizationSeatAvailability.HasAvailableSeats(organization, occupiedSeatCount))
        {
            return new OrganizationHasNoAvailableSeats();
        }

        var seatExpansionError = await TryExpandSeatsAsync(organization, occupiedSeatCount);
        if (seatExpansionError is not null)
        {
            return seatExpansionError;
        }

        var accessSecretsManager = await stripePaymentService.HasSecretsManagerStandalone(organization);
        var newOrganizationUser = new OrganizationUser
        {
            OrganizationId = organization.Id,
            UserId = user.Id,
            Status = OrganizationUserStatusType.Accepted,
            Type = OrganizationUserType.User,
            AccessSecretsManager = accessSecretsManager,
        };
        newOrganizationUser.SetNewId();

        if (autoConfirmPolicyEnabled)
        {
            await deleteEmergencyAccessCommand.DeleteAllByUserIdAsync(user.Id);
        }

        await organizationUserRepository.CreateAsync(newOrganizationUser);

        return newOrganizationUser;
    }

    /// <summary>
    /// Expands the organization's seats before the new membership is created, so that a billing or
    /// persistence failure leaves no orphaned seat. Only auto-adds when the org is already at capacity.
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
            logger.LogWarning(ex, "Could not auto-add seat while accepting invite link for organization {OrganizationId}", organization.Id);
            return new SeatAddFailed();
        }
    }

    private async Task PerformPostAcceptSideEffectsAsync(
        Organization organization, User user, bool autoEnrollEnabled, string? resetPasswordKey, bool autoConfirmPolicyEnabled)
    {
        if (autoEnrollEnabled)
        {
            await updateUserResetPasswordEnrollmentCommand.UpdateUserResetPasswordEnrollmentAsync(
                organization.Id, user.Id, resetPasswordKey, user.Id);
        }

        if (autoConfirmPolicyEnabled)
        {
            await pushAutoConfirmNotificationCommand.PushAsync(user.Id, organization.Id);
        }

        var admins = await organizationUserRepository.GetManyByMinimumRoleAsync(organization.Id, OrganizationUserType.Admin);
        var adminEmails = admins.Select(a => a.Email).Distinct().ToList();
        if (adminEmails.Count > 0)
        {
            await mailService.SendOrganizationAcceptedEmailAsync(organization, user.Email, adminEmails);
        }
    }

    // An email invite can carry an Admin/Owner role. Enforce the "one admin of a Free org"
    // rule here too, mirroring AcceptOrgUserCommand, so the invite link can't bypass it.
    private async Task<Error?> ValidateFreeOrganizationAdminLimitAsync(
        Organization targetOrganization, OrganizationUser existingOrganizationUser, User user)
    {
        if (existingOrganizationUser.Type is OrganizationUserType.Owner or OrganizationUserType.Admin &&
            targetOrganization.PlanType == PlanType.Free &&
            await organizationUserRepository.GetCountByFreeOrganizationAdminUserAsync(user.Id) > 0)
        {
            return new OnlyOneFreeOrganizationAdminAllowed();
        }

        return null;
    }
}
