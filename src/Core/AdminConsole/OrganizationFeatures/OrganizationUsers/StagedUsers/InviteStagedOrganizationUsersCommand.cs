using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Models;
using Bit.Core.AdminConsole.Utilities.v2;
using Bit.Core.AdminConsole.Utilities.v2.Results;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Microsoft.Extensions.Logging;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.StagedUsers;

public class InviteStagedOrganizationUsersCommand(
    IOrganizationRepository organizationRepository,
    IOrganizationUserRepository organizationUserRepository,
    IOrganizationService organizationService,
    ISendOrganizationInvitesCommand sendOrganizationInvitesCommand,
    IEventService eventService,
    TimeProvider timeProvider,
    ILogger<InviteStagedOrganizationUsersCommand> logger)
    : IInviteStagedOrganizationUsersCommand
{
    public async Task<CommandResult<ICollection<OrganizationUser>>> RunAsync(
        InviteStagedOrganizationUsersRequest request)
    {
        var organization = await organizationRepository.GetByIdAsync(request.OrganizationId);
        if (organization is null)
        {
            return new OrganizationNotFound();
        }

        var requestedIds = request.OrganizationUserIds.Distinct().ToList();
        var organizationUsers = (await organizationUserRepository.GetManyAsync(requestedIds)).ToList();

        if (organizationUsers.Count != requestedIds.Count ||
            organizationUsers.Any(organizationUser => organizationUser.OrganizationId != organization.Id))
        {
            return new StagedOrganizationUserNotFound();
        }

        if (organizationUsers.Any(organizationUser => organizationUser.Status != OrganizationUserStatusType.Staged))
        {
            return new OrganizationUserNotStaged();
        }

        var seatReservationError = await ReserveSeatsAsync(organization, organizationUsers.Count);
        if (seatReservationError is not null)
        {
            return seatReservationError;
        }

        await InviteAsync(organizationUsers, organization, request.PerformedBy);

        await eventService.LogOrganizationUserEventsAsync(organizationUsers.Select(organizationUser =>
            (organizationUser, EventType.OrganizationUser_Invited, (DateTime?)organizationUser.RevisionDate)));

        return organizationUsers;
    }

    /// <summary>
    /// Moves each row to Invited and emails the invitations. A send failure restores every row to staged so the
    /// members stay eligible for another attempt; any seats already added to the subscription stay added.
    /// </summary>
    private async Task InviteAsync(List<OrganizationUser> organizationUsers, Organization organization, Guid performedBy)
    {
        var revisionDate = timeProvider.GetUtcNow().UtcDateTime;
        var previousRevisionDates = organizationUsers.ToDictionary(
            organizationUser => organizationUser.Id,
            organizationUser => organizationUser.RevisionDate);

        foreach (var organizationUser in organizationUsers)
        {
            organizationUser.Status = OrganizationUserStatusType.Invited;
            // The update stored procedure persists whatever RevisionDate the entity carries, so bump it here or
            // the row's watermark stays at its staged-creation timestamp and watermark-driven consumers miss the
            // change.
            organizationUser.RevisionDate = revisionDate;
            await organizationUserRepository.ReplaceAsync(organizationUser);
        }

        try
        {
            await sendOrganizationInvitesCommand.SendInvitesAsync(new SendInvitesRequest(
                users: organizationUsers,
                organization: organization,
                initOrganization: false,
                invitingUserId: performedBy));
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failed to send invitations for staged members of organization {OrganizationId}; reverting to staged",
                organization.Id);

            foreach (var organizationUser in organizationUsers)
            {
                organizationUser.Status = OrganizationUserStatusType.Staged;
                organizationUser.RevisionDate = previousRevisionDates[organizationUser.Id];
                await organizationUserRepository.ReplaceAsync(organizationUser);
            }

            throw;
        }
    }

    /// <summary>
    /// Reserves the seats the members occupy once invited. Runs before any row is updated so a billing failure
    /// leaves them staged.
    /// </summary>
    private async Task<Error?> ReserveSeatsAsync(Organization organization, int seatsNeeded)
    {
        if (!organization.Seats.HasValue)
        {
            return null;
        }

        var occupiedSeats = (await organizationRepository.GetOccupiedSeatCountByOrganizationIdAsync(organization.Id)).Total;
        var seatsToAdd = seatsNeeded - (organization.Seats.Value - occupiedSeats);
        if (seatsToAdd <= 0)
        {
            return null;
        }

        // AutoAddSeatsAsync enforces this too, but checking first distinguishes "the cap is in the way" from a
        // payment or gateway failure.
        if (organization.MaxAutoscaleSeats.HasValue &&
            organization.Seats.Value + seatsToAdd > organization.MaxAutoscaleSeats.Value)
        {
            return new NoSeatsAvailableForInvite(organization.DisplayName());
        }

        try
        {
            await organizationService.AutoAddSeatsAsync(organization, seatsToAdd);
            return null;
        }
        catch (Exception ex) when (ex is BadRequestException or GatewayException)
        {
            // Known business failures (no payment method, autoscale cap, etc.) map to a 400.
            // Infrastructure failures propagate so they surface as 5xx with a correlation id.
            logger.LogWarning(ex,
                "Could not auto-add {SeatsToAdd} seat(s) while inviting staged members for organization {OrganizationId}",
                seatsToAdd, organization.Id);
            return new SeatExpansionFailed(organization.DisplayName());
        }
    }
}
