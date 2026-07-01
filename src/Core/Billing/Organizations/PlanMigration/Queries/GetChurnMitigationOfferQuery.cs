using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Extensions;
using Bit.Core.Billing.Organizations.PlanMigration.Models;
using Bit.Core.Billing.Organizations.PlanMigration.Repositories;
using Bit.Core.Billing.Services;
using Microsoft.Extensions.Logging;
using Stripe;

namespace Bit.Core.Billing.Organizations.PlanMigration.Queries;

using static StripeConstants;

public class GetChurnMitigationOfferQuery(
    IOrganizationPlanMigrationCohortAssignmentRepository assignmentRepository,
    IOrganizationPlanMigrationCohortRepository cohortRepository,
    IStripeAdapter stripeAdapter,
    ILogger<GetChurnMitigationOfferQuery> logger) : IGetChurnMitigationOfferQuery
{
    public async Task<ChurnMitigationOfferResult?> Run(Organization organization)
    {
        // DB pre-filter -- short-circuit non-cohort organizations before any Stripe call.
        var assignment = await assignmentRepository.GetByOrganizationIdAsync(organization.Id);
        if (assignment is null)
        {
            return null;
        }

        var cohort = await cohortRepository.GetByIdAsync(assignment.CohortId);
        if (cohort is not { IsActive: true } || string.IsNullOrEmpty(cohort.ChurnDiscountCouponCode))
        {
            return null;
        }

        // Migration cohort: inspect the active subscription schedule -- the coupon goes on
        // Phase 2 only. Churn-only cohort (MigrationPathId is null): inspect live subscription
        // discounts plus the per-assignment one-shot guard for `once` coupons.
        return cohort.MigrationPathId is not null
            ? await EvaluateMigrationCohortAsync(organization, cohort.ChurnDiscountCouponCode)
            : await EvaluateChurnOnlyCohortAsync(organization, assignment, cohort.ChurnDiscountCouponCode);
    }

    private async Task<ChurnMitigationOfferResult?> EvaluateMigrationCohortAsync(
        Organization organization,
        string churnDiscountCouponCode)
    {
        var subscription = await TryGetSubscriptionAsync(organization);
        if (subscription is null)
        {
            return null;
        }

        var schedules = await stripeAdapter.ListSubscriptionSchedulesAsync(
            new SubscriptionScheduleListOptions { Customer = subscription.CustomerId });

        var activeSchedule = schedules.Data.FirstOrDefault(s =>
            s.Status == SubscriptionScheduleStatus.Active && s.SubscriptionId == subscription.Id);

        if (activeSchedule is not { Phases.Count: > 0 })
        {
            return null;
        }

        // Filter out the anchor phase Stripe prepends when a schedule is normalized after a
        // post-attach subscription mutation (e.g. seat add, automatic-tax adjustment). The
        // unexpired-phases view is the canonical [Phase 1, Phase 2] shape this query reasons
        // about; mirrors RedeemChurnMitigationOfferCommand.RedeemForMigrationCohortAsync.
        var now = subscription.TestClock?.FrozenTime ?? DateTime.UtcNow;
        var migrationPhases = activeSchedule.Phases.Where(p => p.EndDate > now).ToList();
        if (migrationPhases.Count != 2)
        {
            return null;
        }

        var phase1 = migrationPhases[0];
        var phase2 = migrationPhases[1];

        // current_phase identifies the in-effect phase. Migration-cohort eligibility requires
        // the org to still be on Phase 1 -- once Stripe rolls into Phase 2 the offer window
        // has closed (the org has been migrated).
        var currentPhase = activeSchedule.CurrentPhase;
        if (currentPhase is null
            || currentPhase.StartDate != phase1.StartDate
            || currentPhase.EndDate != phase1.EndDate)
        {
            return null;
        }

        // The coupon must not already be on Phase 2 (idempotency / re-render guard).
        if (phase2.Discounts is { Count: > 0 } &&
            phase2.Discounts.Any(d => string.Equals(d.CouponId, churnDiscountCouponCode, StringComparison.Ordinal)))
        {
            return null;
        }

        return await TryBuildOfferResultAsync(churnDiscountCouponCode);
    }

    private async Task<ChurnMitigationOfferResult?> EvaluateChurnOnlyCohortAsync(
        Organization organization,
        Entities.OrganizationPlanMigrationCohortAssignment assignment,
        string churnDiscountCouponCode)
    {
        var coupon = await TryGetCouponAsync(churnDiscountCouponCode);
        if (coupon is null)
        {
            return null;
        }

        // Once-only coupons rely on the per-assignment ChurnDiscountAppliedDate as the
        // single post-consumption defense -- the coupon falls off subscription.discounts
        // after the first invoice, so the Stripe-side check below is insufficient on its own.
        if (string.Equals(coupon.Duration, CouponDurations.Once, StringComparison.OrdinalIgnoreCase)
            && assignment.ChurnDiscountAppliedDate.HasValue)
        {
            return null;
        }

        var subscription = await TryGetSubscriptionAsync(organization);
        if (subscription is null)
        {
            return null;
        }

        // Churn-only branch never writes Customer.Discount -- it's managed elsewhere (manual
        // ops adjustments, audience filters via SubscriptionDiscountService). We still inspect
        // it here so an org already carrying this coupon at the customer layer is ineligible.
        if (subscription.Customer?.Discount?.Source?.Coupon?.Id is { Length: > 0 } customerCouponId
            && string.Equals(customerCouponId, churnDiscountCouponCode, StringComparison.Ordinal))
        {
            return null;
        }

        if (subscription.Discounts is { Count: > 0 }
            && subscription.Discounts.Any(d => string.Equals(d.Source?.Coupon?.Id, churnDiscountCouponCode, StringComparison.Ordinal)))
        {
            return null;
        }

        return BuildOfferResult(coupon);
    }

    private async Task<Subscription?> TryGetSubscriptionAsync(Organization organization)
    {
        try
        {
            // `test_clock` is included so the migration-cohort current_phase check is honest
            // against test customers; `discount`/`discounts.coupon` give us the churn-only
            // ineligibility surfaces without a second round-trip.
            return await stripeAdapter.GetSubscriptionAsync(organization.GatewaySubscriptionId,
                new SubscriptionGetOptions
                {
                    Expand = ["customer", "test_clock", "discounts.coupon"]
                });
        }
        catch (StripeException stripeException) when (stripeException.StripeError?.Code == ErrorCodes.ResourceMissing)
        {
            logger.LogWarning(
                "GetChurnMitigationOfferQuery: Subscription ({SubscriptionId}) for Organization ({OrganizationId}) was not found",
                organization.GatewaySubscriptionId, organization.Id);
            return null;
        }
    }

    private async Task<ChurnMitigationOfferResult?> TryBuildOfferResultAsync(string couponId)
    {
        var coupon = await TryGetCouponAsync(couponId);
        return coupon is null ? null : BuildOfferResult(coupon);
    }

    private async Task<Coupon?> TryGetCouponAsync(string couponId)
    {
        // Fail-closed on any Coupons.Get error: a Stripe outage, an ops-deleted coupon, or a
        // typo in the cohort's ChurnDiscountCouponCode all surface to the user as "no offer
        // available this page-load" rather than a Stripe error in the modal.
        try
        {
            return await stripeAdapter.GetCouponAsync(couponId);
        }
        catch (StripeException stripeException)
        {
            logger.LogWarning(
                "GetChurnMitigationOfferQuery: Could not retrieve coupon ({CouponId}) | Code = {Code}",
                couponId, stripeException.StripeError?.Code);
            return null;
        }
    }

    private static ChurnMitigationOfferResult BuildOfferResult(Coupon coupon) =>
        new(
            CouponId: coupon.Id,
            PercentOff: coupon.PercentOff,
            AmountOff: coupon.AmountOff.ToMajor(),
            Duration: coupon.Duration,
            DurationInMonths: (int?)coupon.DurationInMonths,
            Name: coupon.Name);
}
