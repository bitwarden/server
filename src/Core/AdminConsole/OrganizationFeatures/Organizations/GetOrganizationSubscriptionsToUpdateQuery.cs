using Bit.Core.AdminConsole.Models.Data.Organizations;
using Bit.Core.AdminConsole.OrganizationFeatures.Organizations.Interfaces;
using Bit.Core.Billing.Pricing;
using Bit.Core.Repositories;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Organizations;

public class GetOrganizationSubscriptionsToUpdateQuery(IOrganizationRepository organizationRepository,
    IPricingClient pricingClient) : IGetOrganizationSubscriptionsToUpdateQuery
{
    public async Task<IEnumerable<OrganizationSubscriptionUpdate>> GetOrganizationSubscriptionsToUpdateAsync()
    {
        var organizationsToUpdateTask = organizationRepository.GetOrganizationsForSubscriptionSyncAsync();
        var plansTask = pricingClient.ListPlans();

        await Task.WhenAll(organizationsToUpdateTask, plansTask);

        return organizationsToUpdateTask.Result.Select(o => new OrganizationSubscriptionUpdate
        {
            Organization = o,
            Plan = plansTask.Result.FirstOrDefault(plan => plan.Type == o.PlanType)
        });
    }
}
