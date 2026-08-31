using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Models;
using Bit.Core.AdminConsole.Utilities.v2;
using Bit.Core.AdminConsole.Utilities.v2.Results;
using Bit.Core.Billing.Pricing;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Business;
using Bit.Core.OrganizationFeatures.OrganizationSubscriptions.Interface;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Microsoft.Extensions.Logging;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.StagedUsers;

public class InviteStagedOrganizationUsersCommand(
    IOrganizationRepository organizationRepository,
    IOrganizationUserRepository organizationUserRepository,
    IOrganizationService organizationService,
    ICountNewSmSeatsRequiredQuery countNewSmSeatsRequiredQuery,
    IUpdateSecretsManagerSubscriptionCommand updateSecretsManagerSubscriptionCommand,
    IPricingClient pricingClient,
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

        // Password Manager seats first: the subscription rejects more Secrets Manager seats than it has PM seats.
        var secretsManagerSeatReservationError = await ReserveSecretsManagerSeatsAsync(organization, organizationUsers);
        if (secretsManagerSeatReservationError is not null)
        {
            return secretsManagerSeatReservationError;
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
            organizationUser.RevisionDate = revisionDate;
        }

        await organizationUserRepository.ReplaceManyAsync(organizationUsers);

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
            }

            await organizationUserRepository.ReplaceManyAsync(organizationUsers);

            throw;
        }
    }

    /// <summary>
    /// Staged users may be eligible for SM without occupying a seat; enforcement happens at promotion-time.
    /// </summary>
    private async Task<Error?> ReserveSecretsManagerSeatsAsync(Organization organization, List<OrganizationUser> organizationUsers)
    {
        if (!organization.UseSecretsManager)
        {
            return null;
        }

        var membersWithSecretsManager = organizationUsers.Count(organizationUser => organizationUser.AccessSecretsManager);
        if (membersWithSecretsManager == 0)
        {
            return null;
        }

        // Returns 0 for an unlimited plan or when there is already room.
        var seatsToAdd = await countNewSmSeatsRequiredQuery.CountNewSmSeatsRequiredAsync(organization.Id, membersWithSecretsManager);
        if (seatsToAdd <= 0)
        {
            return null;
        }

        try
        {
            var plan = await pricingClient.GetPlanOrThrow(organization.PlanType);
            await updateSecretsManagerSubscriptionCommand.UpdateSubscriptionAsync(
                new SecretsManagerSubscriptionUpdate(organization, plan, true).AdjustSeats(seatsToAdd));
            return null;
        }
        catch (Exception ex) when (ex is BadRequestException or GatewayException)
        {
            logger.LogWarning(ex,
                "Could not add {SeatsToAdd} Secrets Manager seat(s) while inviting staged members for organization {OrganizationId}",
                seatsToAdd, organization.Id);
            return new SecretsManagerSeatExpansionFailed(ex.Message);
        }
    }

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
            return new SeatExpansionFailed(ex.Message);
        }
    }
}
