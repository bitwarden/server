using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Subscriptions.Organization.Commands;
using Bit.Subscriptions.Organization.Queries;

namespace Bit.Subscriptions.Organization.Handlers;

internal sealed class RemovePrivilegedControlsHandler(
    IOrganizationRepository organizationRepository,
    IIsOnPrivilegedControlsSalesInitiatedTrialQuery isOnPrivilegedControlsSalesInitiatedTrialQuery,
    IRemovePrivilegedControlsCommand removePrivilegedControlsCommand)
{
    public async Task HandleAsync(Guid organizationId)
    {
        var organization = await organizationRepository.GetByIdAsync(organizationId)
            ?? throw new NotFoundException();

        // A repeated DELETE after a successful removal is a no-op, not an error.
        if (!organization.UsePam)
        {
            return;
        }

        // The command doesn't detect sales-initiated trials.
        if (await isOnPrivilegedControlsSalesInitiatedTrialQuery.Run(organization))
        {
            throw new BadRequestException(
                "Work with your customer service representative to remove Privileged Controls.");
        }

        await removePrivilegedControlsCommand.Run(organization);
    }
}
