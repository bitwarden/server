using Bit.Core.Billing.Organizations;
using Bit.Core.Billing.Organizations.Commands;
using Bit.Core.Billing.Organizations.Models;
using Bit.Core.Billing.Organizations.Queries;
using Bit.Core.Exceptions;
using Bitwarden.Server.Sdk.Environment;
using Microsoft.Extensions.Logging;
using OrganizationEntity = Bit.Core.AdminConsole.Entities.Organization;

namespace Bit.Subscriptions.Organization.Commands.PrivilegedControls;

internal sealed class ReservePrivilegedControlsSeatsCommand(
    ICountNewPamSeatsRequiredQuery countNewPamSeatsRequiredQuery,
    IAutoscalePrivilegedControlsSeatsCommand autoscalePrivilegedControlsSeatsCommand,
    IBitwardenEnvironment environment,
    ILogger<ReservePrivilegedControlsSeatsCommand> logger) : IReservePrivilegedControlsSeatsCommand
{
    public async Task<PrivilegedControlsSeatReservationResult> RunAsync(OrganizationEntity organization, int seatsRequested)
    {
        // A null seat count means no seat count applies to the organization yet.
        if (seatsRequested < 1 || !organization.UsePam || organization.PamSeats is null)
        {
            return new PrivilegedControlsSeatReservationResult();
        }

        try
        {
            return await ReserveAsync(organization, seatsRequested);
        }
        catch (BadRequestException e)
        {
            logger.LogWarning(e,
                "Could not reserve {SeatsToReserve} Privileged Controls seat(s) for organization {OrganizationId}",
                seatsRequested, organization.Id);
            return new PrivilegedControlsSeatReservationResult(new PrivilegedControlsSeatExpansionFailedError(e.Message), e);
        }
        catch (Exception e)
        {
            logger.LogError(e,
                "Unexpected error reserving {SeatsToReserve} Privileged Controls seat(s) for organization {OrganizationId}",
                seatsRequested, organization.Id);
            return new PrivilegedControlsSeatReservationResult(new PrivilegedControlsSeatReservationUnexpectedError(), e);
        }
    }

    private async Task<PrivilegedControlsSeatReservationResult> ReserveAsync(OrganizationEntity organization, int seatsRequested)
    {
        if (environment.SelfHosted)
        {
            var seatsRequired = await countNewPamSeatsRequiredQuery
                .CountNewPamSeatsRequiredAsync(organization, seatsRequested);

            return seatsRequired > 0
                ? new PrivilegedControlsSeatReservationResult(new CannotAutoscalePrivilegedControlsSeatsOnSelfHostError())
                : new PrivilegedControlsSeatReservationResult();
        }

        // The autoscale command can't add seats to an organization that has none, so zero seats means none are free.
        if (organization.PamSeats < 1)
        {
            return new PrivilegedControlsSeatReservationResult(new PrivilegedControlsSeatLimitReachedError());
        }

        var result = await autoscalePrivilegedControlsSeatsCommand.RunAsync(organization, seatsRequested);

        return result.Success
            ? new PrivilegedControlsSeatReservationResult()
            : new PrivilegedControlsSeatReservationResult(new PrivilegedControlsSeatLimitReachedError());
    }
}
