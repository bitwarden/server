using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Subscriptions.Organization.Commands;
using Bit.Subscriptions.Organization.Models.Requests;
using Bit.Subscriptions.Organization.Queries;

namespace Bit.Subscriptions.Organization.Handlers;

internal sealed class ChangePrivilegedControlsSeatsHandler(
    IOrganizationRepository organizationRepository,
    IIsOnPrivilegedControlsSalesInitiatedTrialQuery isOnPrivilegedControlsSalesInitiatedTrialQuery,
    IChangePrivilegedControlsSeatsCommand changePrivilegedControlsSeatsCommand)
{
    public async Task HandleAsync(Guid organizationId, PrivilegedControlsSeatsRequest request)
    {
        var organization = await organizationRepository.GetByIdAsync(organizationId)
            ?? throw new NotFoundException();

        // The command doesn't detect sales-initiated trials.
        if (await isOnPrivilegedControlsSalesInitiatedTrialQuery.Run(organization))
        {
            throw new BadRequestException(
                "Work with your customer service representative to change your Privileged Controls seats.");
        }

        // The adjustment is applied to the seats the server holds now, not the count the client last saw.
        var seats = organization.PamSeats.GetValueOrDefault() + request.SeatAdjustment.GetValueOrDefault();
        await changePrivilegedControlsSeatsCommand.Run(organization, seats, request.MaxAutoscaleSeats);
    }
}
