using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks.Interfaces;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.UpdateUserResetPasswordEnrollment;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Utilities;
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

namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;

public class AcceptOrganizationInviteLinkCommand(
    IOrganizationInviteLinkRepository organizationInviteLinkRepository,
    IOrganizationRepository organizationRepository,
    IOrganizationUserRepository organizationUserRepository,
    IAcceptInviteLinkMembershipValidator acceptInviteLinkMembershipValidator,
    IOrganizationService organizationService,
    IStripePaymentService stripePaymentService,
    IUpdateUserResetPasswordEnrollmentCommand updateUserResetPasswordEnrollmentCommand,
    IMailService mailService,
    IPushAutoConfirmNotificationCommand pushAutoConfirmNotificationCommand,
    IDeleteEmergencyAccessCommand deleteEmergencyAccessCommand,
    IPolicyQuery policyQuery,
    ILogger<AcceptOrganizationInviteLinkCommand> logger,
    IEventService eventService)
    : IAcceptOrganizationInviteLinkCommand
{
    public async Task<CommandResult<OrganizationUser>> AcceptAsync(AcceptOrganizationInviteLinkRequest request)
    {
        var user = request.User;

        var link = await organizationInviteLinkRepository.GetByOrganizationIdAsync(request.OrganizationId);
        if (link is null || !link.CodeMatches(request.Code.ToString()))
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

        var existingOrganizationUser = await ResolveExistingOrganizationUserAsync(organization, user);

        // These policy states are needed both to validate the accept and to drive the post-accept side effects
        // below, so resolve them here and pass them into the validator on the request.
        var autoConfirmPolicyEnabled = organization.UsePolicies
            && (await policyQuery.RunAsync(organization.Id, PolicyType.AutomaticUserConfirmation)).Enabled;
        var autoEnrollEnabled = await IsAccountRecoveryAutoEnrollEnabledAsync(organization);

        var membershipValidationResult = await acceptInviteLinkMembershipValidator.ValidateAsync(
            new AcceptInviteLinkMembershipValidationRequest
            {
                Organization = organization,
                User = user,
                AllowedDomains = link.GetAllowedDomains(),
                ExistingMembership = existingOrganizationUser,
                ResetPasswordKey = request.ResetPasswordKey,
                AutoConfirmPolicyEnabled = autoConfirmPolicyEnabled,
                AccountRecoveryAutoEnroll = autoEnrollEnabled,
            });
        if (membershipValidationResult.IsError)
        {
            return membershipValidationResult.AsError;
        }

        var acceptResult = existingOrganizationUser is not null
            ? await AcceptExistingOrgUserAsync(organization, existingOrganizationUser, user, autoConfirmPolicyEnabled)
            : await CreateNewMembershipAsync(organization, user, autoConfirmPolicyEnabled);
        if (acceptResult.IsError)
        {
            return acceptResult;
        }

        await eventService.LogOrganizationUserEventAsync(acceptResult.AsSuccess, EventType.OrganizationUser_InviteLinkAccepted);

        await PerformPostAcceptSideEffectsAsync(organization, user, autoEnrollEnabled, request.ResetPasswordKey, autoConfirmPolicyEnabled);

        return acceptResult;
    }

    private async Task<bool> IsAccountRecoveryAutoEnrollEnabledAsync(Organization organization)
    {
        if (!organization.UsePolicies)
        {
            return false;
        }

        var resetPasswordPolicy = await policyQuery.RunAsync(organization.Id, PolicyType.ResetPassword);
        return resetPasswordPolicy.Enabled
            && resetPasswordPolicy.GetDataModel<ResetPasswordDataModel>().AutoEnrollEnabled;
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

    private async Task<CommandResult<OrganizationUser>> AcceptExistingOrgUserAsync(
        Organization organization, OrganizationUser existingOrganizationUser, User user, bool autoConfirmPolicyEnabled)
    {
        if (existingOrganizationUser.Status == OrganizationUserStatusType.Staged)
        {
            var seatReservationError = await ReserveSeatAsync(organization);
            if (seatReservationError is not null)
            {
                return seatReservationError;
            }
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
        var seatReservationError = await ReserveSeatAsync(organization);
        if (seatReservationError is not null)
        {
            return seatReservationError;
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
    /// Reserves capacity for one more seat-occupying member. Runs before the membership is written so that a
    /// billing or persistence failure leaves no orphaned seat.
    /// </summary>
    private async Task<Error?> ReserveSeatAsync(Organization organization)
    {
        var occupiedSeatCount = (await organizationRepository.GetOccupiedSeatCountByOrganizationIdAsync(organization.Id)).Total;
        if (!OrganizationSeatAvailability.HasAvailableSeats(organization, occupiedSeatCount))
        {
            return new OrganizationHasNoAvailableSeats(organization.DisplayName());
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
}
