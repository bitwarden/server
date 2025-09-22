using System.Collections.Immutable;
using Bit.Core;
using Bit.Core.AdminConsole.OrganizationFeatures.Organizations.Interfaces;
using Bit.Core.Jobs;
using Bit.Core.Services;
using Quartz;

namespace Bit.Api.AdminConsole.Jobs;

public class OrganizationSubscriptionUpdateJob(ILogger<OrganizationSubscriptionUpdateJob> logger,
    IGetOrganizationSubscriptionsToUpdateQuery query,
    IUpdateOrganizationSubscriptionCommand command,
    IFeatureService featureService) : BaseJob(logger)
{
    protected override async Task ExecuteJobAsync(IJobExecutionContext _)
    {
        if (!featureService.IsEnabled(FeatureFlagKeys.ScimInviteUserOptimization))
        {
            return;
        }

        logger.LogInformation("OrganizationSubscriptionUpdateJob - START");

        var organizationSubscriptionsToUpdate =
            (await query.GetOrganizationSubscriptionsToUpdateAsync())
            .ToImmutableList();

        logger.LogInformation("OrganizationSubscriptionUpdateJob - {numberOfOrganizations} organization(s) to update",
            organizationSubscriptionsToUpdate.Count);

        await command.UpdateOrganizationSubscriptionAsync(organizationSubscriptionsToUpdate);

        logger.LogInformation("OrganizationSubscriptionUpdateJob - COMPLETED");
    }
}
