#nullable enable

using Bit.Core.Billing.Subscriptions.Entities;
using Bit.Core.Entities;

namespace Bit.Core.Billing.Services.DiscountAudienceFilters;

/// <summary>
/// Restricts a discount to users who have never held a Bitwarden subscription.
/// A user is considered to have no previous subscriptions when they are not currently
/// a Premium member and have no recorded Stripe subscription ID.
/// </summary>
public class UserHasNoPreviousSubscriptionsFilter : IDiscountAudienceFilter
{
    public bool IsUserEligible(User user, SubscriptionDiscount discount) =>
        !user.Premium && string.IsNullOrEmpty(user.GatewaySubscriptionId);
}
