using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Organizations.Commands;
using Bit.Core.Billing.Organizations.Queries;
using Bit.Core.Settings;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.PrivilegedControlsSeats;

public class ReservePrivilegedControlsSeatsCommand(
    ICountNewPamSeatsRequiredQuery countNewPamSeatsRequiredQuery,
    IAutoscalePrivilegedControlsSeatsCommand autoscalePrivilegedControlsSeatsCommand,
    IGlobalSettings globalSettings) : IReservePrivilegedControlsSeatsCommand
{
    public async Task<PrivilegedControlsSeatReservation> RunAsync(Organization organization, int membersTakingSeats)
    {
        if (membersTakingSeats < 1 || !organization.UsePam || organization.PamSeats is null)
        {
            return PrivilegedControlsSeatReservation.Reserved;
        }

        if (globalSettings.SelfHosted)
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
