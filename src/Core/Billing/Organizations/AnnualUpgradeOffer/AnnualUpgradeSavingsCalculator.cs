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
/// The two invoice previews the quote compares: the same quantities priced once on the
/// subscription's current monthly price IDs and once on their annual equivalents. Both sides carry
/// the same invoice-level coupons: the subscription's own when it has any, otherwise the customer's.
/// Only forever coupons are modeled, since a temporary one will not exist at renewal.
/// </summary>
internal readonly record struct AnnualUpgradePreviewRequests(
    InvoiceCreatePreviewOptions Monthly,
    InvoiceCreatePreviewOptions Annual);

/// <summary>
/// Prices a monthly-to-annual switch by asking Stripe to preview two invoices from the
/// subscription's own line items: one on the current monthly price IDs and one on their annual
/// equivalents, with the same quantities on both. Both previews carry the same invoice-level
/// coupons: the subscription's own when it has any, otherwise the customer's. Only forever coupons
/// are modeled, since a temporary one will not exist at renewal. Stripe reports the discount it
/// actually applied per line.
/// </summary>
internal static class AnnualUpgradeSavingsCalculator
{
    /// <summary>
    /// Builds the two preview payloads: the same quantities priced once on the subscription's
    /// current price ids and once on the annual equivalents in <paramref name="lines"/>. Both sides
    /// carry the same invoice-level coupons: the subscription's own when it has any, otherwise the
    /// customer's. Only forever coupons are modeled, since a temporary one will not exist at renewal.
    /// </summary>
    /// <remarks>
    /// The caller must load the subscription with <c>customer</c>, <c>discounts.coupon</c>,
    /// <c>customer.discount.coupon</c>, and <c>items.data.discounts.coupon</c> expanded.
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
                (discount?.Coupon).IsForever() &&
                !string.IsNullOrEmpty(discount!.Coupon.Id))
            .Select(discount => new InvoiceSubscriptionDetailsItemDiscountOptions
            {
                Coupon = discount.Coupon.Id
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
            .Where(discount => discount?.Coupon is not null)
            .Select(discount => discount.Coupon)
            .ToList();

        if (subscriptionCoupons.Count > 0)
        {
            return subscriptionCoupons;
        }

        var customerCoupon = subscription.Customer?.Discount?.Coupon;
        return customerCoupon is null ? [] : [customerCoupon];
    }

    // Only forever coupons: a temporary one would quote a first-year artifact as a recurring saving.
    private static List<InvoiceDiscountOptions> ApplicableDiscountOptions(IReadOnlyList<Coupon> coupons) =>
        [.. coupons
            .Where(coupon => coupon.IsForever() && !string.IsNullOrEmpty(coupon.Id))
            .Select(coupon => new InvoiceDiscountOptions { Coupon = coupon.Id })];
}
