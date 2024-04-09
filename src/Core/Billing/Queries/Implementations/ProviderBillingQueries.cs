using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Models;
using Bit.Core.Billing.Repositories;
using Bit.Core.Enums;
using Bit.Core.Utilities;
using Microsoft.Extensions.Logging;
using Stripe;
using static Bit.Core.Billing.Utilities;

namespace Bit.Core.Billing.Queries.Implementations;

public class ProviderBillingQueries(
    ILogger<ProviderBillingQueries> logger,
    IProviderOrganizationRepository providerOrganizationRepository,
    IProviderPlanRepository providerPlanRepository,
    IProviderRepository providerRepository,
    ISubscriberQueries subscriberQueries) : IProviderBillingQueries
{
    public async Task<int> GetAssignedSeatTotalForPlanOrThrow(
        Guid providerId,
        PlanType planType)
    {
        var provider = await providerRepository.GetByIdAsync(providerId);

        if (provider == null)
        {
            logger.LogError(
                "Could not find provider ({ID}) when retrieving assigned seat total",
                providerId);

            throw ContactSupport();
        }

        if (provider.Type == ProviderType.Reseller)
        {
            logger.LogError("Assigned seats cannot be retrieved for reseller-type provider ({ID})", providerId);

            throw ContactSupport("Consolidated billing does not support reseller-type providers");
        }

        var providerOrganizations = await providerOrganizationRepository.GetManyDetailsByProviderAsync(providerId);

        var plan = StaticStore.GetPlan(planType);

        return providerOrganizations
            .Where(providerOrganization => providerOrganization.Plan == plan.Name)
            .Sum(providerOrganization => providerOrganization.Seats ?? 0);
    }

    public async Task<ProviderSubscriptionDTO> GetSubscriptionDTO(Guid providerId)
    {
        var provider = await providerRepository.GetByIdAsync(providerId);

        if (provider == null)
        {
            logger.LogError(
                "Could not find provider ({ID}) when retrieving subscription data.",
                providerId);

            return null;
        }

        if (provider.Type == ProviderType.Reseller)
        {
            logger.LogError("Subscription data cannot be retrieved for reseller-type provider ({ID})", providerId);

            throw ContactSupport("Consolidated billing does not support reseller-type providers");
        }

        var subscription = await subscriberQueries.GetSubscription(provider, new SubscriptionGetOptions
        {
            Expand = ["customer"]
        });

        if (subscription == null)
        {
            return null;
        }

        var providerPlans = await providerPlanRepository.GetByProviderId(providerId);

        var configuredProviderPlans = providerPlans
            .Where(providerPlan => providerPlan.IsConfigured())
            .Select(ConfiguredProviderPlanDTO.From)
            .ToList();

        return new ProviderSubscriptionDTO(
            configuredProviderPlans,
            subscription);
    }
}
