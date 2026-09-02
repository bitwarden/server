using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Organizations.AnnualUpgradeOffer.Models;

namespace Bit.Core.Billing.Organizations.AnnualUpgradeOffer.Queries;

/// <summary>
/// Computes the monthly-vs-annual-latest savings offer shown in the cancellation flow when an
/// organization selects a cost-related cancellation reason. Excludes organizations in a cohort
/// eligible for the (mutually exclusive) churn-mitigation coupon offer, and organizations not on
/// a recognized monthly business plan.
/// </summary>
public interface IGetAnnualUpgradeOfferQuery
{
    Task<AnnualUpgradeOfferResult?> Run(Organization organization);
}
