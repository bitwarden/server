using Stripe;

namespace Bit.Core.Billing.Models;

public record ConsolidatedBillingSubscriptionDTO(
    List<ConfiguredProviderPlanDTO> ProviderPlans,
    Subscription Subscription);
