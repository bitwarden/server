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
/// Prices a monthly-to-annual switch from the subscription's own line items. Quantities come from
/// Stripe; unit prices come from the plan catalog on both sides, so the difference reflects the
/// cadence change rather than a mismatch between two data sources.
/// </summary>
internal static class AnnualUpgradeSavingsCalculator
{
    private readonly record struct Line(SubscriptionItem Item, PlanPriceMapping Mapping);

    /// <summary>
    /// Returns null when the subscription has no line items, or when any line has no annual
    /// equivalent. A line the redemption cannot map is a line the redemption will refuse, so
    /// quoting a figure for it would advertise an offer that cannot be taken.
    /// </summary>
    /// <remarks>
    /// The caller must load the subscription with <c>customer</c>, <c>discounts</c>, and
    /// <c>items.data.discounts</c> expanded. Stripe deserializes an unexpanded discount collection
    /// as a non-null list of null elements, which this method cannot distinguish from having no
    /// discounts at all. An unexpanded payload would therefore silently degrade to a list-price
    /// quote, and because it would also read as zero subscription-level coupons, it could let a
    /// customer-level coupon that Stripe actually suppresses leak into the calculation.
    /// </remarks>
    public static AnnualUpgradeSavings? Calculate(
        Subscription subscription, Plan currentPlan, Plan annualLatestPlan)
    {
        var lines = new List<Line>();

        foreach (var item in subscription.Items.Data)
        {
            // Stripe.NET can surface a line with no price object; the previous implementation
            // skipped these rather than treating them as unpriceable, so keep doing that.
            if (item.Price?.Id is null)
            {
                continue;
            }

            var mapping = OrganizationPlanMigrationPriceMapper.MapWithPricesOrNull(
                item.Price.Id, currentPlan, annualLatestPlan);
            if (mapping is null)
            {
                return null;
            }

            lines.Add(new Line(item, mapping.Value));
        }

        if (lines.Count == 0)
        {
            return null;
        }

        var currency = subscription.Currency;
        var invoiceCoupons = InvoiceLevelCoupons(subscription);
        var monthlyInvoice = InvoiceTotal(lines, invoiceCoupons, currency, m => m.SourceUnitPrice);
        var annualInvoice = InvoiceTotal(lines, invoiceCoupons, currency, m => m.TargetUnitPrice);

        // A fixed-amount coupon is deducted once per invoice regardless of billing interval, so
        // the comparison has to be built from one invoice and then multiplied, never the reverse.
        return new AnnualUpgradeSavings(monthlyInvoice * 12, annualInvoice);
    }

    /// <summary>
    /// The invoice-level coupons in force. Stripe gates customer inheritance on the subscription
    /// having no discounts of its own ("When a subscription has no discounts, the customer-level
    /// discount, if any, applies to invoices"), so the two never stack. Both sides of the quote
    /// use this one set: the redemption is discount-neutral, carrying the same effective coupons
    /// into Phase 2 that the subscription bills under today.
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

    private static decimal InvoiceTotal(
        IReadOnlyList<Line> lines,
        IReadOnlyList<Coupon> invoiceCoupons,
        string currency,
        Func<PlanPriceMapping, decimal> unitPrice)
    {
        // Rounded here and after every discount below, mirroring Stripe striking each line to
        // integer minor units as it goes. Rounding only once, at the end, would let a sub-cent
        // residual compound across stacked coupons and then be amplified twelvefold by the
        // monthly-to-annual multiply in Calculate.
        var amounts = lines
            .Select(line => RoundToCents(unitPrice(line.Mapping) * line.Item.Quantity))
            .ToArray();

        // Stripe applies line item discounts before invoice discounts, and the invoice subtotal is
        // struck with item discounts already incorporated.
        for (var i = 0; i < lines.Count; i++)
        {
            foreach (var discount in lines[i].Item.Discounts ?? [])
            {
                var coupon = discount?.Coupon;
                if (!IsApplicable(coupon, currency))
                {
                    continue;
                }

                amounts[i] = RoundToCents(coupon!.PercentOff is { } percentOff
                    ? amounts[i] * (1 - percentOff / 100m)
                    : Math.Max(0m, amounts[i] - AmountOff(coupon)));
            }
        }

        foreach (var coupon in invoiceCoupons)
        {
            if (!IsApplicable(coupon, currency))
            {
                continue;
            }

            var scope = Enumerable.Range(0, lines.Count)
                .Where(i => InScope(coupon, lines[i].Item))
                .ToArray();

            var scopedAmount = scope.Sum(i => amounts[i]);
            if (scope.Length == 0 || scopedAmount <= 0m)
            {
                continue;
            }

            var deduction = coupon.PercentOff is { } percentOff
                ? scopedAmount * (percentOff / 100m)
                : Math.Min(AmountOff(coupon), scopedAmount);

            // Allocate proportionally so scoping stays well defined for any coupon that follows,
            // matching how Stripe spreads an invoice-level discount across line items.
            foreach (var i in scope)
            {
                amounts[i] = RoundToCents(amounts[i] - deduction * (amounts[i] / scopedAmount));
            }
        }

        return amounts.Sum();
    }

    /// <summary>
    /// Two decimal places, halves rounded away from zero: the commercial convention, and the
    /// opposite of the banker's rounding .NET defaults to, so it is passed explicitly. Stripe's
    /// exact midpoint behaviour was not verified against its documentation; this is a deliberate
    /// choice on our part, not a proven match, and a future reader should not assume otherwise.
    /// </summary>
    private static decimal RoundToCents(decimal amount) =>
        Math.Round(amount, 2, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Only coupons that will still be running at renewal are modelled. A <c>once</c> coupon is
    /// consumed long before, and a <c>repeating</c> coupon discounts an entire annual invoice
    /// against only a few monthly ones, which would quote a first-year artifact as a recurring
    /// saving.
    /// </summary>
    private static bool IsApplicable(Coupon? coupon, string currency) =>
        coupon is not null &&
        string.Equals(coupon.Duration, CouponDurations.Forever, StringComparison.OrdinalIgnoreCase) &&
        (coupon.AmountOff is null || string.Equals(coupon.Currency, currency, StringComparison.OrdinalIgnoreCase));

    private static decimal AmountOff(Coupon coupon) => (coupon.AmountOff ?? 0L) / 100m;

    private static bool InScope(Coupon coupon, SubscriptionItem item) =>
        coupon.AppliesTo?.Products is not { Count: > 0 } products ||
        products.Contains(item.Price.ProductId);
}
