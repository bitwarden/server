using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks.Interfaces;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.UpdateUserResetPasswordEnrollment;
using Bit.Core.AdminConsole.Utilities;
using Bit.Core.AdminConsole.Utilities.v2;
using Bit.Core.AdminConsole.Utilities.v2.Results;
using Bit.Core.Auth.UserFeatures.EmergencyAccess.Interfaces;
using Bit.Core.Billing.Organizations;
using Bit.Core.Billing.Organizations.Commands;
using Bit.Core.Billing.Organizations.Models;
using Bit.Core.Billing.Services;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Microsoft.Extensions.Logging;

namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;

public class AcceptOrganizationInviteLinkCommand(
    IOrganizationRepository organizationRepository,
    IOrganizationUserRepository organizationUserRepository,
    IAcceptInviteLinkMembershipValidator acceptInviteLinkMembershipValidator,
    IOrganizationService organizationService,
    IStripePaymentService stripePaymentService,
    IUpdateUserResetPasswordEnrollmentCommand updateUserResetPasswordEnrollmentCommand,
    IMailService mailService,
    IPushAutoConfirmNotificationCommand pushAutoConfirmNotificationCommand,
    IDeleteEmergencyAccessCommand deleteEmergencyAccessCommand,
    IReservePrivilegedControlsSeatsCommand reservePrivilegedControlsSeatsCommand,
    ISendPrivilegedControlsSeatNeededEmailCommand sendPrivilegedControlsSeatNeededEmailCommand,
    ILogger<AcceptOrganizationInviteLinkCommand> logger,
    IEventService eventService)
    : IAcceptOrganizationInviteLinkCommand
{
    public async Task<CommandResult<OrganizationUser>> AcceptAsync(AcceptOrganizationInviteLinkRequest request)
    {
        var user = request.User;

        var membershipValidationResult = await acceptInviteLinkMembershipValidator.ValidateAsync(
            new AcceptInviteLinkMembershipValidationRequest
            {
                OrganizationId = request.OrganizationId,
                Code = request.Code,
                User = user,
                ResetPasswordKey = request.ResetPasswordKey,
            });
        if (membershipValidationResult.IsError)
        {
            return membershipValidationResult.AsError;
        }

        var validationResult = membershipValidationResult.AsSuccess;
        var existingOrganizationUser = validationResult.ExistingOrganizationUser;

        var acceptResult = existingOrganizationUser is not null
            ? await AcceptExistingOrgUserAsync(validationResult, existingOrganizationUser, user)
            : await CreateNewMembershipAsync(validationResult, user);
        if (acceptResult.IsError)
        {
            return acceptResult;
        }

        await PerformPostAcceptSideEffectsAsync(request, validationResult, acceptResult.AsSuccess);

        return acceptResult;
    }

    private async Task<CommandResult<OrganizationUser>> AcceptExistingOrgUserAsync(
        AcceptInviteLinkMembershipValidationResult validationResult,
        OrganizationUser existingOrganizationUser,
        User user)
    {
        var organization = validationResult.Organization;

        PrivilegedControlsSeatReservationResult? privilegedControlsSeatReservation = null;
        if (existingOrganizationUser.Status == OrganizationUserStatusType.Staged)
        {
            var seatReservationError = await ReserveSeatAsync(organization);
            if (seatReservationError is not null)
            {
                return seatReservationError;
            }

            privilegedControlsSeatReservation = await reservePrivilegedControlsSeatsCommand.RunAsync(
                organization, existingOrganizationUser.AccessPam ? 1 : 0);
            if (!privilegedControlsSeatReservation.Success)
            {
                existingOrganizationUser.AccessPam = false;
            }
        }

        var memberEmail = existingOrganizationUser.Email ?? user.Email;
        existingOrganizationUser.Status = OrganizationUserStatusType.Accepted;
        existingOrganizationUser.UserId = user.Id;
        existingOrganizationUser.Email = null;
        existingOrganizationUser.ClearInviteReminders();

        if (validationResult.AutoConfirmPolicyEnabled)
        {
            await deleteEmergencyAccessCommand.DeleteAllByUserIdAsync(user.Id);
        }

        await organizationUserRepository.ReplaceAsync(existingOrganizationUser);

        if (privilegedControlsSeatReservation?.Error is PrivilegedControlsSeatLimitReachedError)
        {
            await sendPrivilegedControlsSeatNeededEmailCommand.RunAsync(organization, memberEmail);
        }

        return existingOrganizationUser;
    }

    private async Task<CommandResult<OrganizationUser>> CreateNewMembershipAsync(
        AcceptInviteLinkMembershipValidationResult validationResult, User user)
    {
        var organization = validationResult.Organization;

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

        if (validationResult.AutoConfirmPolicyEnabled)
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
        AcceptOrganizationInviteLinkRequest request,
        AcceptInviteLinkMembershipValidationResult validationResult,
        OrganizationUser organizationUser)
    {
        var user = request.User;
        var organization = validationResult.Organization;

        await eventService.LogOrganizationUserEventAsync(organizationUser, EventType.OrganizationUser_InviteLinkAccepted);

        if (validationResult.AccountRecoveryAutoEnrollEnabled)
        {
            await updateUserResetPasswordEnrollmentCommand.UpdateUserResetPasswordEnrollmentAsync(
                organization.Id, user.Id, request.ResetPasswordKey, user.Id);
        }

        if (validationResult.AutoConfirmPolicyEnabled)
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
