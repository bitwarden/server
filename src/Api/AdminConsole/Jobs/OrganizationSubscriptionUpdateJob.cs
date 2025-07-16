using Bit.Core;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Extensions;
using Bit.Core.Billing.Pricing;
using Bit.Core.Jobs;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Quartz;

namespace Bit.Api.AdminConsole.Jobs;

public class OrganizationSubscriptionUpdateJob(ILogger<OrganizationSubscriptionUpdateJob> logger,
    IPaymentService paymentService,
    IPricingClient pricingClient,
    IOrganizationRepository organizationRepository,
    IOrganizationSubscriptionUpdateRepository repository,
    IFeatureService featureService) : BaseJob(logger)
{
    protected override async Task ExecuteJobAsync(IJobExecutionContext context)
    {
        if (!featureService.IsEnabled(FeatureFlagKeys.ScimInviteUserOptimization))
        {
            return;
        }

        logger.LogInformation("OrganizationSubscriptionUpdateJob - START");

        var subscriptionUpdates = await repository.GetUpdatesToSubscriptionAsync();

        var organizationIds = subscriptionUpdates.Select(x => x.OrganizationId)
            .Distinct()
            .ToArray();

        logger.LogInformation("OrganizationSubscriptionUpdateJob - {numberOfOrganizations} organization(s) to update",
            organizationIds.Length);

        var listPlansTask = pricingClient.ListPlans();
        var getOrganizationsTask = organizationRepository.GetManyByIdsAsync(organizationIds);

        await Task.WhenAll(listPlansTask, getOrganizationsTask);

        var plans = listPlansTask.Result;
        var organizations = getOrganizationsTask.Result;

        var successfulSyncs = new List<Guid>();
        var failedSyncs = new List<Guid>();

        foreach (var organization in organizations)
        {
            try
            {
                if (organization.Seats.HasValue)
                {
                    await paymentService.AdjustSeatsAsync(organization,
                        plans.GetPlan(organization.PlanType),
                        organization.Seats.Value);

                    successfulSyncs.Add(organization.Id);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "OrganizationSubscriptionUpdateJob - Failed for organization {organizationId}",
                    organization.Id);

                failedSyncs.Add(organization.Id);
            }
        }

        await repository.UpdateSubscriptionStatusAsync(successfulSyncs, failedSyncs);

        logger.LogInformation("OrganizationSubscriptionUpdateJob - COMPLETED");
    }
}
