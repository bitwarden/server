using Bit.Core.Billing.Organizations.PlanMigration;
using Stripe;
using static Bit.Core.Billing.Constants.StripeConstants;
using Plan = Bit.Core.Models.StaticStore.Plan;

namespace Bit.Core.Billing.Organizations.AnnualUpgradeOffer;

/// <summary>
/// The two figures the annual upgrade offer quotes: what the organization spends on twelve
/// monthly invoices today, and what one annual invoice would cost after the switch.
/// </summary>
internal readonly record struct AnnualUpgradeSavings(decimal CurrentAnnualCost, decimal NewAnnualCost);

/// <summary>
/// The two invoice previews the quote compares: the same quantities and the same coupons priced
/// once on the subscription's current monthly price IDs and once on their annual equivalents.
/// </summary>
internal readonly record struct AnnualUpgradePreviewRequests(
    InvoiceCreatePreviewOptions Monthly,
    InvoiceCreatePreviewOptions Annual);

/// <summary>
/// Prices a monthly-to-annual switch by asking Stripe to preview two invoices from the
/// subscription's own line items: one on the current monthly price IDs and one on their annual
/// equivalents, with the same quantities and the same coupons on both. Stripe reports the discount
/// it actually applied per line.
/// </summary>
internal static class AnnualUpgradeSavingsCalculator
{
    private readonly record struct PreviewLine(SubscriptionItem Item, string TargetPriceId);

    /// <summary>
    /// Builds the two preview payloads, or null when the subscription has no line items or when any
    /// line has no annual equivalent. A line the redemption cannot map is a line the redemption
    /// will refuse, so quoting a figure for it would advertise an offer that cannot be taken.
    /// </summary>
    /// <remarks>
    /// The caller must load the subscription with <c>customer</c>, <c>discounts.coupon</c>,
    /// <c>customer.discount.coupon</c>, and <c>items.data.discounts.coupon</c> expanded.
    /// </remarks>
    public static AnnualUpgradePreviewRequests? BuildPreviewRequestsOrNull(
        Subscription subscription, Plan currentPlan, Plan annualLatestPlan)
    {
        var lines = new List<PreviewLine>();

        foreach (var item in subscription.Items.Data)
        {
            if (item.Price?.Id is null)
            {
                continue;
            }

            var targetPriceId = OrganizationPlanMigrationPriceMapper.MapOrNull(
                item.Price.Id, currentPlan, annualLatestPlan);
            if (targetPriceId is null)
            {
                return null;
            }

            lines.Add(new PreviewLine(item, targetPriceId));
        }

        if (lines.Count == 0)
        {
            return null;
        }

        var invoiceDiscounts = InvoiceLevelCoupons(subscription)
            .Where(coupon => IsApplicable(coupon) && !string.IsNullOrEmpty(coupon.Id))
            .Select(coupon => new InvoiceDiscountOptions { Coupon = coupon.Id })
            .ToList();

        return new AnnualUpgradePreviewRequests(
            BuildPreviewOptions(subscription, lines, invoiceDiscounts, annual: false),
            BuildPreviewOptions(subscription, lines, invoiceDiscounts, annual: true));
    }

    private static InvoiceCreatePreviewOptions BuildPreviewOptions(
        Subscription subscription,
        IReadOnlyList<PreviewLine> lines,
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
            Discounts = invoiceDiscounts.Count > 0 ? invoiceDiscounts : null
        };

    private static List<InvoiceSubscriptionDetailsItemDiscountOptions>? ItemDiscountsOrNull(
        PreviewLine line)
    {
        var discounts = (line.Item.Discounts ?? [])
            .Where(discount =>
                IsApplicable(discount?.Coupon) &&
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
    /// missing, which suppresses the offer.
    /// </summary>
    public static AnnualUpgradeSavings? SavingsFromPreviews(Invoice? monthly, Invoice? annual)
    {
        if (monthly is null || annual is null)
        {
            return null;
        }

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

    /// <summary>
    /// Only coupons that will still be running at renewal are modelled. A <c>once</c> coupon is
    /// consumed long before, and a <c>repeating</c> coupon discounts an entire annual invoice
    /// against only a few monthly ones, which would quote a first-year artifact as a recurring
    /// saving.
    /// </summary>
    private static bool IsApplicable(Coupon? coupon) =>
        coupon is not null &&
        string.Equals(coupon.Duration, CouponDurations.Forever, StringComparison.OrdinalIgnoreCase);
}
