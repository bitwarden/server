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

        // An omitted seat count is forwarded as 0 so the factory rejects it with its "at least one seat" message.
        await changePrivilegedControlsSeatsCommand.Run(organization, request.Seats ?? 0, request.MaxAutoscaleSeats);
    }
}
