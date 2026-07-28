using Bit.Core.Billing.Organizations.PlanMigration;
using Stripe;
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

        var monthlyInvoice = InvoiceTotal(lines, m => m.SourceUnitPrice);
        var annualInvoice = InvoiceTotal(lines, m => m.TargetUnitPrice);

        // A fixed-amount coupon is deducted once per invoice regardless of billing interval, so
        // the comparison has to be built from one invoice and then multiplied, never the reverse.
        return new AnnualUpgradeSavings(monthlyInvoice * 12, annualInvoice);
    }

    private static decimal InvoiceTotal(
        IReadOnlyList<Line> lines, Func<PlanPriceMapping, decimal> unitPrice) =>
        lines.Sum(line => unitPrice(line.Mapping) * line.Item.Quantity);
}
