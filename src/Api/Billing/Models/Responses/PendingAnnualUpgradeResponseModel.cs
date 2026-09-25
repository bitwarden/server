using Bit.Api.Models.Response;
using Bit.Core.Billing.Organizations.AnnualUpgradeOffer.Models;
using static Bit.Api.Models.Response.BillingSubscription;

namespace Bit.Api.Billing.Models.Responses;

/// <summary>
/// The scheduled annual-upgrade target for an organization that redeemed the annual-upgrade offer
/// but whose monthly-to-annual Stripe schedule has not yet activated.
/// </summary>
public class PendingAnnualUpgradeResponseModel
{
    public PendingAnnualUpgradeResponseModel(PendingAnnualUpgrade pendingAnnualUpgrade)
    {
        Plan = new PlanResponseModel(pendingAnnualUpgrade.Plan);
        LineItems = [.. pendingAnnualUpgrade.LineItems.Select(lineItem => new BillingSubscriptionItem(lineItem))];
        EffectiveDate = pendingAnnualUpgrade.EffectiveDate;
    }

    public PlanResponseModel Plan { get; set; }

    /// <summary>
    /// Null when the caller may not see sensitive billing data. See
    /// <c>OrganizationSubscriptionResponseModel</c>'s hideSensitiveData branch.
    /// </summary>
    public List<BillingSubscriptionItem>? LineItems { get; set; }

    public DateTime EffectiveDate { get; set; }
}
