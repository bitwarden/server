#nullable enable

using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Subscriptions.Entities;

namespace Bit.Core.Billing.Models.Api.Response;

public class SubscriptionDiscountResponseModel
{
    public string StripeCouponId { get; init; } = null!;
    public IEnumerable<string>? StripeProductIds { get; init; }
    public decimal? PercentOff { get; init; }
    public long? AmountOff { get; init; }
    public string? Currency { get; init; }
    public string Duration { get; init; } = null!;
    public int? DurationInMonths { get; init; }
    public string? Name { get; init; }
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
    public IDictionary<DiscountTierType, bool>? TierEligibility { get; init; }

    public static SubscriptionDiscountResponseModel From(
        SubscriptionDiscount discount,
        IDictionary<DiscountTierType, bool> tierEligibility) => new()
        {
            StripeCouponId = discount.StripeCouponId,
            StripeProductIds = discount.StripeProductIds,
            PercentOff = discount.PercentOff,
            AmountOff = discount.AmountOff,
            Currency = discount.Currency,
            Duration = discount.Duration,
            DurationInMonths = discount.DurationInMonths,
            Name = discount.Name,
            StartDate = discount.StartDate,
            EndDate = discount.EndDate,
            TierEligibility = tierEligibility
        };
}
