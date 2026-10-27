using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.PrivilegedControlsSeats;
using Bit.Core.Billing.Organizations.Commands;
using Bit.Core.Billing.Organizations.Queries;
using Bitwarden.Server.Sdk.Environment;
using OrganizationEntity = Bit.Core.AdminConsole.Entities.Organization;

namespace Bit.Subscriptions.Organization.Commands;

internal sealed class ReservePrivilegedControlsSeatsCommand(
    ICountNewPamSeatsRequiredQuery countNewPamSeatsRequiredQuery,
    IAutoscalePrivilegedControlsSeatsCommand autoscalePrivilegedControlsSeatsCommand,
    IBitwardenEnvironment environment) : IReservePrivilegedControlsSeatsCommand
{
    public async Task<PrivilegedControlsSeatReservation> RunAsync(OrganizationEntity organization, int membersTakingSeats)
    {
        if (membersTakingSeats < 1 || !organization.UsePam || organization.PamSeats is null or < 1)
        {
            return PrivilegedControlsSeatReservation.Reserved;
        }

        if (environment.SelfHosted)
        {
            var seatsRequired = await countNewPamSeatsRequiredQuery
                .CountNewPamSeatsRequiredAsync(organization, membersTakingSeats);

            return seatsRequired > 0
                ? PrivilegedControlsSeatReservation.SelfHostedShortfall
                : PrivilegedControlsSeatReservation.Reserved;
        }

        var result = await autoscalePrivilegedControlsSeatsCommand.Run(organization, membersTakingSeats);

        return result.Success
            ? PrivilegedControlsSeatReservation.Reserved
            : PrivilegedControlsSeatReservation.LimitReached;
    }
}
