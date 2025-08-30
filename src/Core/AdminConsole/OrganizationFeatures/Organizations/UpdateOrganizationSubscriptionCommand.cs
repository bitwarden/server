using Bit.Core.AdminConsole.Models.Data.Organizations;
using Bit.Core.AdminConsole.OrganizationFeatures.Organizations.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Microsoft.Extensions.Logging;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Organizations;

public class UpdateOrganizationSubscriptionCommand(IPaymentService paymentService,
    IOrganizationRepository repository,
    TimeProvider timeProvider,
    ILogger<UpdateOrganizationSubscriptionCommand> logger) : IUpdateOrganizationSubscriptionCommand
{
    public async Task UpdateOrganizationSubscriptionAsync(IEnumerable<OrganizationSubscriptionUpdate> subscriptionsToUpdate)
    {
        var successfulSyncs = new List<Guid>();

        foreach (var subscriptionUpdate in subscriptionsToUpdate)
        {
            try
            {
                await paymentService.AdjustSeatsAsync(subscriptionUpdate.Organization,
                    subscriptionUpdate.Plan,
                    subscriptionUpdate.Seats);

                successfulSyncs.Add(subscriptionUpdate.Organization.Id);
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Failed to update organization {organizationId} subscription.",
                    subscriptionUpdate.Organization.Id);
            }
        }

        if (successfulSyncs.Count == 0)
        {
            return;
        }

        await repository.UpdateSuccessfulOrganizationSyncStatusAsync(successfulSyncs, timeProvider.GetUtcNow().UtcDateTime);
    }
}
