using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Models;
using Bit.Core.AdminConsole.Utilities.v2;
using Bit.Core.AdminConsole.Utilities.v2.Results;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Services;
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
    IStripePaymentService stripePaymentService,
    IPricingClient pricingClient,
    ISendOrganizationInvitesCommand sendOrganizationInvitesCommand,
    IEventService eventService,
    TimeProvider timeProvider,
    ILogger<InviteStagedOrganizationUsersCommand> logger)
    : IInviteStagedOrganizationUsersCommand
{
    public async Task<BulkCommandResultCollection> RunAsync(InviteStagedOrganizationUsersRequest request)
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

        var (eligible, skipped) = GetEligibleStagedUsers(requestedIds, organizationUsersById);

        if (eligible.Count == 0)
        {
            return BuildResults(requestedIds, skipped);
        }

        await GrantSecretsManagerStandaloneAccessAsync(organization, eligible);

        var seatsToAdd = await CountSeatsToAddAsync(organization, eligible.Count);
        var secretsManagerSeatsToAdd = await CountSecretsManagerSeatsToAddAsync(organization, eligible);

        var secretsManagerValidationError = await ValidateSecretsManagerSeatsAsync(organization, seatsToAdd, secretsManagerSeatsToAdd);
        if (secretsManagerValidationError is not null)
        {
            return secretsManagerValidationError;
        }

        var seatReservationError = await ReserveSeatsAsync(organization, seatsToAdd);
        if (seatReservationError is not null)
        {
            return seatReservationError;
        }

        // Captured before the subscription grows, because a successful update writes the new total onto the entity.
        var initialSecretsManagerSeats = organization.SmSeats;

        // Password Manager seats first: the subscription rejects more Secrets Manager seats than it has PM seats.
        var secretsManagerSeatReservationError = await AdjustSecretsManagerSeatsAsync(organization, secretsManagerSeatsToAdd, dryRun: false);
        if (secretsManagerSeatReservationError is not null)
        {
            await ReleaseSeatsAsync(organization, seatsToAdd);
            return secretsManagerSeatReservationError;
        }

        try
        {
            await InviteAsync(eligible, organization, request.PerformedBy);
        }
        catch
        {
            await ReleaseSecretsManagerSeatsAsync(organization, secretsManagerSeatsToAdd, initialSecretsManagerSeats);
            await ReleaseSeatsAsync(organization, seatsToAdd);
            throw;
        }

        await eventService.LogOrganizationUserEventsAsync(eligible.Select(organizationUser =>
            (organizationUser, EventType.OrganizationUser_Invited, (DateTime?)organizationUser.RevisionDate)));

        return BuildResults(requestedIds, skipped);
    }

    /// <summary>
    /// Splits the requested members into those that can be invited and those that cannot.
    /// </summary>
    /// <remarks>
    /// </remarks>
    private static (List<OrganizationUser> Eligible, Dictionary<Guid, Error> Skipped) GetEligibleStagedUsers(
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
    /// members stay eligible for another attempt.
    /// </summary>
    /// <remarks>
    /// The caller unwinds both subscriptions when this throws, so a failed send leaves no seat paid for. Secrets
    /// Manager access granted by the standalone discount is kept: it reflects the organization's entitlement
    /// rather than this invitation, and a staged row occupies no Secrets Manager seat.
    /// </remarks>
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
    /// Grants Secrets Manager access to every eligible member when the organization carries the Secrets Manager
    /// standalone discount, which entitles the whole organization rather than individually chosen members.
    /// </summary>
    /// <remarks>
    /// Runs before the seats are counted so the members it grants are paid for. Access is only ever added: a
    /// member provisioned with it keeps it either way. The call short-circuits without reaching Stripe when the
    /// organization has no Secrets Manager or no gateway customer.
    /// </remarks>
    private async Task GrantSecretsManagerStandaloneAccessAsync(
        Organization organization,
        List<OrganizationUser> organizationUsers)
    {
        if (!await stripePaymentService.HasSecretsManagerStandalone(organization))
        {
            return;
        }

        foreach (var organizationUser in organizationUsers)
        {
            organizationUser.AccessSecretsManager = true;
        }
    }

    /// <summary>
    /// Counts the Password Manager seats the eligible members need beyond what the subscription already has room for.
    /// </summary>
    private async Task<int> CountSeatsToAddAsync(Organization organization, int seatsNeeded)
    {
        if (!organization.Seats.HasValue)
        {
            return 0;
        }

        var occupiedSeats = (await organizationRepository.GetOccupiedSeatCountByOrganizationIdAsync(organization.Id)).Total;
        return Math.Max(seatsNeeded - (organization.Seats.Value - occupiedSeats), 0);
    }

    /// <summary>
    /// Staged users may be eligible for SM without occupying a seat; enforcement happens at promotion-time.
    /// </summary>
    private async Task<int> CountSecretsManagerSeatsToAddAsync(Organization organization, List<OrganizationUser> organizationUsers)
    {
        if (!organization.UseSecretsManager)
        {
            return 0;
        }

        var membersWithSecretsManager = organizationUsers.Count(organizationUser => organizationUser.AccessSecretsManager);
        if (membersWithSecretsManager == 0)
        {
            return 0;
        }

        // Returns 0 for an unlimited plan or when there is already room.
        return Math.Max(
            await countNewSmSeatsRequiredQuery.CountNewSmSeatsRequiredAsync(organization.Id, membersWithSecretsManager),
            0);
    }

    /// <summary>
    /// Dry runs the Secrets Manager seat adjustment against the seat total the Password Manager reservation is
    /// about to produce, so a rejection surfaces before anything is charged.
    /// </summary>
    private async Task<Error?> ValidateSecretsManagerSeatsAsync(
        Organization organization,
        int seatsToAdd,
        int secretsManagerSeatsToAdd)
    {
        if (secretsManagerSeatsToAdd <= 0)
        {
            return null;
        }

        // Validation compares org seats to SM seats, so we need to temporarily "elevate" the org seats
        var currentSeats = organization.Seats;
        organization.Seats = currentSeats + seatsToAdd;

        try
        {
            return await AdjustSecretsManagerSeatsAsync(organization, secretsManagerSeatsToAdd, dryRun: true);
        }
        finally
        {
            organization.Seats = currentSeats;
        }
    }

    /// <summary>
    /// Builds the Secrets Manager seat adjustment and either validates it or applies it. Both paths share one
    /// builder deliberately, because a dry run assembled differently from the real call would prove nothing.
    /// </summary>
    /// <param name="dryRun">Runs only the validation half: nothing is charged and no seat total is written.</param>
    private async Task<Error?> AdjustSecretsManagerSeatsAsync(
        Organization organization,
        int secretsManagerSeatsToAdd,
        bool dryRun)
    {
        if (secretsManagerSeatsToAdd <= 0)
        {
            return null;
        }

        try
        {
            var plan = await pricingClient.GetPlanOrThrow(organization.PlanType);
            var update = new SecretsManagerSubscriptionUpdate(organization, plan, true)
                .AdjustSeats(secretsManagerSeatsToAdd);

            if (dryRun)
            {
                await updateSecretsManagerSubscriptionCommand.ValidateUpdateAsync(update);
            }
            else
            {
                await updateSecretsManagerSubscriptionCommand.UpdateSubscriptionAsync(update);
            }

            return null;
        }
        catch (Exception ex) when (ex is BadRequestException or GatewayException)
        {
            logger.LogWarning(ex,
                "Could not add {SeatsToAdd} Secrets Manager seat(s) while inviting staged members for organization {OrganizationId}",
                secretsManagerSeatsToAdd, organization.Id);
            return new SecretsManagerSeatExpansionFailed(ex.Message);
        }
    }

    private async Task<Error?> ReserveSeatsAsync(Organization organization, int seatsToAdd)
    {
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

    /// <summary>
    /// Restores the Secrets Manager seat count captured before the seats were added.
    /// </summary>
    /// <remarks>
    /// Autoscaling is off for the revert because the subscription refuses to subtract seats while it is on.
    /// </remarks>
    private async Task ReleaseSecretsManagerSeatsAsync(
        Organization organization,
        int secretsManagerSeatsAdded,
        int? initialSecretsManagerSeats)
    {
        if (secretsManagerSeatsAdded <= 0)
        {
            return;
        }

        try
        {
            var plan = await pricingClient.GetPlanOrThrow(organization.PlanType);
            await updateSecretsManagerSubscriptionCommand.UpdateSubscriptionAsync(
                new SecretsManagerSubscriptionUpdate(organization, plan, false)
                {
                    SmSeats = initialSecretsManagerSeats
                });
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failed to release {SeatsToRelease} Secrets Manager seat(s) for organization {OrganizationId}",
                secretsManagerSeatsAdded, organization.Id);
        }
    }

    /// <summary>
    /// Hands back seats added by <see cref="ReserveSeatsAsync"/> when a later step means nobody will be invited.
    /// </summary>
    private async Task ReleaseSeatsAsync(Organization organization, int seatsToRelease)
    {
        if (seatsToRelease <= 0)
        {
            return;
        }

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
}
