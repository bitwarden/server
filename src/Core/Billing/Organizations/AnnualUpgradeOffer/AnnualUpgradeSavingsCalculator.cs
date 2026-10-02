using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Extensions;
using Bit.Core.Billing.Organizations.AnnualUpgradeOffer.Models;
using Stripe;

namespace Bit.Core.Billing.Organizations.AnnualUpgradeOffer;

/// <summary>
/// The two figures the annual upgrade offer quotes: what the organization spends on twelve
/// monthly invoices today, and what one annual invoice would cost after the switch.
/// </summary>
internal readonly record struct AnnualUpgradeSavings(decimal CurrentAnnualCost, decimal NewAnnualCost);

/// <summary>
/// The monthly and annual invoice previews the quote compares.
/// </summary>
internal readonly record struct AnnualUpgradePreviewRequests(
    InvoiceCreatePreviewOptions Monthly,
    InvoiceCreatePreviewOptions Annual);

/// <summary>
/// Prices a monthly-to-annual switch from two Stripe invoice previews of the subscription's line
/// items, one on the current monthly prices and one on their annual equivalents.
/// </summary>
internal static class AnnualUpgradeSavingsCalculator
{
    /// <summary>
    /// Builds the monthly and annual preview payloads.
    /// </summary>
    /// <remarks>
    /// The caller must load the subscription with <c>customer</c>, <c>discounts.source.coupon</c>,
    /// <c>customer.discount.source.coupon</c>, and <c>items.data.discounts.source</c> expanded.
    /// </remarks>
    public static AnnualUpgradePreviewRequests BuildPreviewRequests(
        Subscription subscription, IReadOnlyList<AnnualUpgradeLine> lines)
    {
        return new AnnualUpgradePreviewRequests(
            BuildPreviewOptions(subscription, lines, ApplicableDiscountOptions(InvoiceLevelCoupons(subscription)), annual: false),
            BuildPreviewOptions(subscription, lines, ApplicableDiscountOptions(InvoiceLevelCoupons(subscription)), annual: true));
    }

    private static InvoiceCreatePreviewOptions BuildPreviewOptions(
        Subscription subscription,
        IReadOnlyList<AnnualUpgradeLine> lines,
        List<InvoiceDiscountOptions> invoiceDiscounts,
        bool annual) =>
        new()
        {
            Customer = subscription.CustomerId,
            // Pre-tax on both sides. With tax off, Total is the post-discount figure.
            AutomaticTax = new InvoiceAutomaticTaxOptions { Enabled = false },
            // No Subscription set on purpose. Passing one would price with prorations, not the full term.
            SubscriptionDetails = new InvoiceSubscriptionDetailsOptions
            {
                BillingMode = new InvoiceSubscriptionDetailsBillingModeOptions { Type = StripeConstants.BillingMode.Classic },
                Items = [.. lines.Select(line => new InvoiceSubscriptionDetailsItemOptions
                {
                    Price = annual ? line.TargetPriceId : line.Item.Price.Id,
                    Quantity = line.Item.Quantity,
                    Discounts = ItemDiscountsOrNull(line)
                })]
            },
            // Empty list serializes to `discounts=`, Stripe's documented opt-out from inheriting.
            Discounts = invoiceDiscounts
        };

    private static List<InvoiceSubscriptionDetailsItemDiscountOptions>? ItemDiscountsOrNull(
        AnnualUpgradeLine line)
    {
        var discounts = (line.Item.Discounts ?? [])
            .Where(discount =>
                (discount?.Source?.Coupon).IsForever() &&
                !string.IsNullOrEmpty(discount!.Source?.Coupon?.Id))
            .Select(discount => new InvoiceSubscriptionDetailsItemDiscountOptions
            {
                Coupon = discount.Source?.Coupon?.Id
            })
            .ToList();

        return discounts.Count > 0 ? discounts : null;
    }

    /// <summary>
    /// Calculates the savings from the two preview totals. Returns null when either preview is
    /// missing, which suppresses the offer. Uses <c>Total</c>, not <c>TotalExcludingTax</c>, which
    /// Stripe can leave null on an untaxed invoice and would suppress every offer.
    /// </summary>
    public static AnnualUpgradeSavings? SavingsFromPreviews(Invoice? monthly, Invoice? annual)
    {
        if (monthly is null || annual is null)
        {
            return null;
        }

        // Multiplying the monthly total, not dividing the annual one: a fixed-amount coupon is
        // deducted once per invoice regardless of billing interval.
        return new AnnualUpgradeSavings(monthly.Total / 100m * 12, annual.Total / 100m);
    }

    /// <summary>
    /// The invoice-level coupons in force: the subscription's own when it has any, otherwise the
    /// customer's, which Stripe never stacks.
    /// </summary>
    private static IReadOnlyList<Coupon> InvoiceLevelCoupons(Subscription subscription)
    {
        var subscriptionCoupons = (subscription.Discounts ?? [])
            .Where(discount => discount?.Source?.Coupon is not null)
            .Select(discount => discount.Source!.Coupon)
            .ToList();

        if (subscriptionCoupons.Count > 0)
        {
            return subscriptionCoupons;
        }

        var customerCoupon = subscription.Customer?.Discount?.Source?.Coupon;
        return customerCoupon is null ? [] : [customerCoupon];
    }

    // Only forever coupons: a temporary one would quote a first-year artifact as a recurring saving.
    private static List<InvoiceDiscountOptions> ApplicableDiscountOptions(IReadOnlyList<Coupon> coupons) =>
        [.. coupons
            .Where(coupon => coupon.IsForever() && !string.IsNullOrEmpty(coupon.Id))
            .Select(coupon => new InvoiceDiscountOptions { Coupon = coupon.Id })];
}
