using Bit.Core.AdminConsole.OrganizationFeatures.Organizations.Interfaces;
using Bit.Core.Billing.Organizations.Commands;
using Bit.Core.Billing.Organizations.Models;
using Bit.Core.Billing.Services;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Microsoft.Extensions.Logging;
using OrganizationSubscriptionUpdate = Bit.Core.AdminConsole.Models.Data.Organizations.OrganizationSubscriptionUpdate;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Organizations;

public class BulkUpdateOrganizationSubscriptionsCommand(
    IStripePaymentService paymentService,
    IOrganizationRepository repository,
    TimeProvider timeProvider,
    ILogger<BulkUpdateOrganizationSubscriptionsCommand> logger,
    IFeatureService featureService,
    IUpdateOrganizationSubscriptionCommand updateOrganizationSubscriptionCommand) : IBulkUpdateOrganizationSubscriptionsCommand
{
    public async Task BulkUpdateOrganizationSubscriptionsAsync(IEnumerable<OrganizationSubscriptionUpdate> subscriptionsToUpdate)
    {
        var successfulSyncs = new List<Guid>();
        var useUpdateOrganizationSubscriptionCommand =
            featureService.IsEnabled(FeatureFlagKeys.PM32581_UseUpdateOrganizationSubscriptionCommand);

        foreach (var subscriptionUpdate in subscriptionsToUpdate)
        {
            if (useUpdateOrganizationSubscriptionCommand)
            {
                var changeSet = OrganizationSubscriptionChangeSet.UpdatePasswordManagerSeats(
                    subscriptionUpdate.Plan!,
                    subscriptionUpdate.Seats);

                var result =
                    await updateOrganizationSubscriptionCommand.Run(subscriptionUpdate.Organization, changeSet);

                if (result.Success)
                {
                    successfulSyncs.Add(subscriptionUpdate.Organization.Id);
                }
                else
                {
                    logger.LogError("Failed to update organization {OrganizationId} subscription.", subscriptionUpdate.Organization.Id);
                }
            }
            else
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
        }

        if (successfulSyncs.Count == 0)
        {
            return;
        }

        await repository.UpdateSuccessfulOrganizationSyncStatusAsync(successfulSyncs, timeProvider.GetUtcNow().UtcDateTime);
    }
}
