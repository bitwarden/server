using Stripe;

namespace Bit.Core.Billing.Models;

public record ProviderSubscriptionDTO(
    List<ConfiguredProviderPlanDTO> ProviderPlans,
    Subscription Subscription);
