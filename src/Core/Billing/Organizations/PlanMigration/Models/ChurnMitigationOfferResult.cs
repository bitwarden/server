namespace Bit.Core.Billing.Organizations.PlanMigration.Models;

/// <summary>
/// Primitive description of the churn-mitigation save offer that the web vault should
/// present to an eligible organization. The server intentionally returns only structured
/// data -- modal copy is composed client-side (PM-37173).
/// </summary>
/// <param name="CouponId">The Stripe coupon id that will be applied if the offer is redeemed.</param>
/// <param name="PercentOff">
/// The coupon's percent-off value. Nullable to forward-compatibly accommodate amount-off
/// coupons, even though the operator-controlled validation in PM-36951 normally rejects them.
/// </param>
/// <param name="Duration">
/// The Stripe coupon's <c>duration</c> string (<c>once</c>, <c>repeating</c>, or <c>forever</c>).
/// Drives whether the once-only per-assignment guard applies on the churn-only branch -- see
/// <c>RedeemChurnMitigationOfferCommand</c> for the branch-specific atomic-CAS-then-Stripe
/// order used to defend a single redemption of a <c>once</c> coupon.
/// </param>
/// <param name="DurationInMonths">The coupon's repeating duration in months; null unless <see cref="Duration"/> is <c>repeating</c>.</param>
/// <param name="Name">The coupon's display name.</param>
public sealed record ChurnMitigationOfferResult(
    string CouponId,
    decimal? PercentOff,
    string Duration,
    int? DurationInMonths,
    string Name);
