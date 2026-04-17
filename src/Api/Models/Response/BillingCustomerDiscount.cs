using Bit.Core.Models.Business;

namespace Bit.Api.Models.Response;

/// <summary>
/// Customer discount information from Stripe billing.
/// </summary>
public class BillingCustomerDiscount
{
    /// <summary>
    /// The Stripe coupon ID (e.g., "cm3nHfO1").
    /// </summary>
    public string? Id { get; }

    /// <summary>
    /// Whether the discount is a recurring/perpetual discount with no expiration date.
    /// <para>
    /// This property is true only when the discount has no end date, meaning it applies
    /// indefinitely to all future renewals. This is a product decision for Milestone 2
    /// to only display perpetual discounts in the UI.
    /// </para>
    /// <para>
    /// Note: This does NOT indicate whether the discount is "currently active" in the billing sense.
    /// A discount with a future end date is functionally active and will be applied by Stripe,
    /// but this property will be false because it has an expiration date.
    /// </para>
    /// </summary>
    public bool Active { get; }

    /// <summary>
    /// Percentage discount applied to the subscription (e.g., 20.0 for 20% off).
    /// Null if this is an amount-based discount.
    /// </summary>
    public decimal? PercentOff { get; }

    /// <summary>
    /// Fixed amount discount in USD (e.g., 14.00 for $14 off).
    /// Converted from Stripe's cent-based values (1400 cents → $14.00).
    /// Null if this is a percentage-based discount.
    /// Note: Stripe stores amounts in the smallest currency unit. This value is always in USD.
    /// </summary>
    public decimal? AmountOff { get; }

    /// <summary>
    /// List of Stripe product IDs that this discount applies to (e.g., ["prod_premium", "prod_families"]).
    /// <para>
    /// Null: discount applies to all products with no restrictions (AppliesTo not specified in Stripe).
    /// Empty list: discount restricted to zero products (edge case - AppliesTo.Products = [] in Stripe).
    /// Non-empty list: discount applies only to the specified product IDs.
    /// </para>
    /// </summary>
    public IReadOnlyList<string>? AppliesTo { get; }

    /// <summary>
    /// Creates a BillingCustomerDiscount from a SubscriptionInfo.BillingCustomerDiscount.
    /// </summary>
    /// <param name="discount">The discount to convert. Must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when discount is null.</exception>
    public BillingCustomerDiscount(SubscriptionInfo.BillingCustomerDiscount discount)
    {
        ArgumentNullException.ThrowIfNull(discount);

        Id = discount.Id;
        Active = discount.Active;
        PercentOff = discount.PercentOff;
        AmountOff = discount.AmountOff;
        AppliesTo = discount.AppliesTo;
    }
}
