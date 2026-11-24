using Bit.Core;
using Bit.Core.Billing.Organizations;
using Bit.Core.Billing.Organizations.Commands;
using Bit.Core.Billing.Organizations.Models;
using Bit.Core.Billing.Organizations.Queries;
using Bit.Core.Exceptions;
using Bitwarden.Server.Sdk.Environment;
using Bitwarden.Server.Sdk.Features;
using Microsoft.Extensions.Logging;
using OrganizationEntity = Bit.Core.AdminConsole.Entities.Organization;

namespace Bit.Subscriptions.Organization.Commands.PrivilegedControls;

internal sealed class ReservePrivilegedControlsSeatsCommand(
    IValidatePrivilegedControlsSeatReservationQuery validateSeatReservationQuery,
    IAutoscalePrivilegedControlsSeatsCommand autoscalePrivilegedControlsSeatsCommand,
    IBitwardenEnvironment environment,
    IFeatureService featureService,
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
        var error = await validateSeatReservationQuery.RunAsync(organization, seatsRequested, passwordManagerSeatsToAdd: 0);
        if (error is not null)
        {
            return new PrivilegedControlsSeatReservationResult(error);
        }

        // Self-hosted and sales-assisted (flag off) organizations never autoscale; the validation above is the whole check.
        if (environment.SelfHosted || !featureService.IsEnabled(FeatureFlagKeys.PM39883_Pam_Billing_Support))
        {
            return new PrivilegedControlsSeatReservationResult();
        }

        var result = await autoscalePrivilegedControlsSeatsCommand.RunAsync(organization, seatsRequested);

        return result.Success
            ? new PrivilegedControlsSeatReservationResult()
            : new PrivilegedControlsSeatReservationResult(new PrivilegedControlsSeatLimitReachedError());
    }
}
