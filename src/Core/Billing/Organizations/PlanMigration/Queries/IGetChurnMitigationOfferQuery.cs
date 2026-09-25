using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Organizations.PlanMigration.Models;

namespace Bit.Core.Billing.Organizations.PlanMigration.Queries;

/// <summary>
/// Decides whether an organization is currently eligible for a churn-mitigation save offer
/// and, if so, returns the primitive coupon shape the web vault needs to render the modal.
/// </summary>
/// <remarks>
/// The eligibility predicate is a DB pre-filter (assignment + cohort + non-null
/// <c>ChurnDiscountCouponCode</c>) followed by a fork on <c>cohort.MigrationPathId</c>:
/// migration cohorts inspect the active subscription schedule, churn-only cohorts inspect
/// the live subscription discounts plus the per-assignment one-shot guard. Returns <c>null</c>
/// for every ineligible path so callers can distinguish "no offer" from "service error".
/// </remarks>
public interface IGetChurnMitigationOfferQuery
{
    Task<ChurnMitigationOfferResult?> Run(Organization organization);
}
