using Bit.Core.Models.StaticStore;

namespace Bit.Core.Billing.Organizations.AnnualUpgradeOffer.Models;

/// <summary>
/// The scheduled annual-upgrade target for an organization that has redeemed the annual-upgrade
/// churn offer but whose monthly-to-annual Stripe schedule has not yet activated. Null unless a
/// redeemed schedule is present; see <see cref="Queries.GetPendingAnnualUpgradeQuery"/>.
/// </summary>
public record PendingAnnualUpgrade
{
    public required Plan Plan { get; init; }
    public required IReadOnlyList<PendingAnnualUpgradeLineItem> LineItems { get; init; }
    public required DateTime EffectiveDate { get; init; }
}

public record PendingAnnualUpgradeLineItem
{
    public string? Name { get; init; }
    public decimal Amount { get; init; }
    public int Quantity { get; init; }
    public string? Interval { get; init; }
    public string? ProductId { get; init; }
    public bool AddonSubscriptionItem { get; init; }
}
