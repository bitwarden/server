using Bit.Core.Billing.Organizations.Commands;
using Bit.Core.Billing.Organizations.Helpers;
using Bit.Core.Exceptions;
using Bit.Core.Services;
using Bit.Subscriptions.Organization.Queries;
using Bitwarden.Server.Sdk.Environment;
using OrganizationEntity = Bit.Core.AdminConsole.Entities.Organization;

namespace Bit.Subscriptions.Organization.Commands;

internal sealed class RevertPrivilegedControlsSeatsCommand(
    IPrivilegedControlsSeatChangeSetFactory seatChangeSetFactory,
    IUpdateOrganizationSubscriptionCommand updateOrganizationSubscriptionCommand,
    IOrganizationService organizationService,
    IIsOnPrivilegedControlsSalesInitiatedTrialQuery isOnTrialQuery,
    IBitwardenEnvironment environment) : IRevertPrivilegedControlsSeatsCommand
{
    public async Task Run(OrganizationEntity organization, int seats)
    {
        if (environment.SelfHosted)
        {
            throw new BadRequestException("Cannot update subscription on a self-hosted instance.");
        }

        if (seats == organization.PamSeats)
        {
            return;
        }

        if (!await isOnTrialQuery.Run(organization))
        {
            var seatChange = await seatChangeSetFactory.CreateAsync(organization, seats);
            (await updateOrganizationSubscriptionCommand.Run(organization, seatChange.ChangeSet)).GetValueOrThrowHttpException();
        }

        organization.PamSeats = seats;
        await organizationService.ReplaceAndUpdateCacheAsync(organization);
    }
}
