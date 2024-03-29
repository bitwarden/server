using Stripe;

namespace Bit.Core.Billing.Models;

public record ProviderSubscriptionData(
    List<ConfiguredProviderPlan> ProviderPlans,
    Subscription Subscription);
