using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Subscriptions.Entities;

namespace Bit.Core.Billing.Models;

/// <summary>
/// Pairs a <see cref="SubscriptionDiscount"/> with its per-tier eligibility matrix.
/// </summary>
public record DiscountEligibility(
    SubscriptionDiscount Discount,
    IDictionary<DiscountTierType, bool> TierEligibility);
