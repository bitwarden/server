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
using None = OneOf.Types.None;

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
    public async Task<CommandResult<ICollection<BulkCommandResult>>> RunAsync(InviteStagedOrganizationUsersRequest request)
    {
        var organization = await organizationRepository.GetByIdAsync(request.OrganizationId);
        if (organization is null)
        {
            return new OrganizationNotFound();
        }

        var requestedIds = request.OrganizationUserIds.Distinct().ToList();
        var organizationUsersById = (await organizationUserRepository.GetManyAsync(requestedIds))
            .Where(organizationUser => organizationUser.OrganizationId == organization.Id)
            .ToDictionary(organizationUser => organizationUser.Id);

        var (eligible, skipped) = Partition(requestedIds, organizationUsersById);

        if (eligible.Count == 0)
        {
            return BuildResults(requestedIds, skipped);
        }

        var seatReservationError = await ReserveSeatsAsync(organization, eligible.Count);
        if (seatReservationError is not null)
        {
            return seatReservationError;
        }

        // Password Manager seats first: the subscription rejects more Secrets Manager seats than it has PM seats.
        var secretsManagerSeatReservationError = await ReserveSecretsManagerSeatsAsync(organization, eligible);
        if (secretsManagerSeatReservationError is not null)
        {
            return secretsManagerSeatReservationError;
        }

        await InviteAsync(eligible, organization, request.PerformedBy);

        await eventService.LogOrganizationUserEventsAsync(eligible.Select(organizationUser =>
            (organizationUser, EventType.OrganizationUser_Invited, (DateTime?)organizationUser.RevisionDate)));

        return BuildResults(requestedIds, skipped);
    }

    /// <summary>
    /// Splits the requested members into those that can be invited and those that cannot.
    /// </summary>
    /// <remarks>
    /// Ineligible members are reported per row instead of failing the request, so one member who was promoted
    /// or removed since the admin loaded the members grid cannot block everyone else's invitation. Seat
    /// expansion stays all-or-nothing because it is reserved once for the whole eligible set.
    /// </remarks>
    private static (List<OrganizationUser> Eligible, Dictionary<Guid, Error> Skipped) Partition(
        List<Guid> requestedIds,
        Dictionary<Guid, OrganizationUser> organizationUsersById)
    {
        var eligible = new List<OrganizationUser>();
        var skipped = new Dictionary<Guid, Error>();

        foreach (var requestedId in requestedIds)
        {
            if (!organizationUsersById.TryGetValue(requestedId, out var organizationUser))
            {
                skipped[requestedId] = new StagedOrganizationUserNotFound();
            }
            else if (organizationUser.Status != OrganizationUserStatusType.Staged)
            {
                skipped[requestedId] = new OrganizationUserNotStaged();
            }
            else
            {
                eligible.Add(organizationUser);
            }
        }

        return (eligible, skipped);
    }

    private static List<BulkCommandResult> BuildResults(List<Guid> requestedIds, Dictionary<Guid, Error> skipped)
        => requestedIds
            .Select(requestedId => new BulkCommandResult(requestedId, skipped.TryGetValue(requestedId, out var error)
                ? new CommandResult(error)
                : new CommandResult(new None())))
            .ToList();

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
