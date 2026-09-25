using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Commands;
using OneOf.Types;

namespace Bit.Core.Billing.Organizations.PlanMigration.Commands;

/// <summary>
/// Applies a churn-mitigation cohort's churn coupon to the appropriate Stripe surface for
/// an organization that has just been offered the save deal.
/// </summary>
/// <remarks>
/// Re-validates eligibility via <see cref="Queries.IGetChurnMitigationOfferQuery"/> as the
/// first step so a redemption never lands on an org whose eligibility window has closed
/// between modal-load and Accept. Order-of-operations is branch-specific:
/// <list type="bullet">
/// <item>
/// Migration cohort -- Stripe-first (set-union the coupon onto Phase 2 of the active
/// schedule), then write <c>ChurnDiscountAppliedDate</c>. Set-union semantics make a
/// re-attempted redeem a no-op at the Stripe layer.
/// </item>
/// <item>
/// Churn-only cohort -- atomic CAS on <c>ChurnDiscountAppliedDate IS NULL</c> first
/// (the only post-consumption defense for <c>once</c> coupons), then Stripe
/// <c>UpdateSubscriptionAsync</c> with the coupon set-unioned onto <c>subscription.Discounts</c>.
/// </item>
/// </list>
/// </remarks>
public interface IRedeemChurnMitigationOfferCommand
{
    Task<BillingCommandResult<None>> Run(Organization organization);
}
