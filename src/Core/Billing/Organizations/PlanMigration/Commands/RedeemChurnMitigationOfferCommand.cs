using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Commands;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Organizations.PlanMigration.Queries;
using Bit.Core.Billing.Organizations.PlanMigration.Repositories;
using Bit.Core.Billing.Services;
using Microsoft.Extensions.Logging;
using OneOf.Types;
using Stripe;

namespace Bit.Core.Billing.Organizations.PlanMigration.Commands;

using static StripeConstants;

public class RedeemChurnMitigationOfferCommand(
    ILogger<RedeemChurnMitigationOfferCommand> logger,
    IGetChurnMitigationOfferQuery getOfferQuery,
    IOrganizationPlanMigrationCohortAssignmentRepository assignmentRepository,
    IOrganizationPlanMigrationCohortRepository cohortRepository,
    IStripeAdapter stripeAdapter)
    : BaseBillingCommand<RedeemChurnMitigationOfferCommand>(logger), IRedeemChurnMitigationOfferCommand
{
    // Stripe error code/message hints used to map the webhook race (Phase 1 just advanced to
    // Phase 2 between revalidation and our schedule update) into a clean BadRequest. Stripe
    // returns this as an invalid_request_error with phrasing along the lines of
    // "Cannot modify a phase of subscription schedule that is in the past" / "phase that has
    // already started" -- match a few human-readable hint substrings inside the message.
    private static readonly string[] _cannotModifyPastPhaseHints =
    [
        "in the past",
        "already started",
        "past phase"
    ];

    private readonly ILogger<RedeemChurnMitigationOfferCommand> _logger = logger;

    protected override Conflict DefaultConflict =>
        new("We had a problem applying your discount. Please contact support for assistance.");

    public Task<BillingCommandResult<None>> Run(Organization organization) => HandleAsync<None>(async () =>
    {
        // Re-validate eligibility through the same query the GET endpoint uses. Inheriting
        // the FF gate and predicate from the query is intentional -- there is no separate
        // command-side gate. If the offer is no longer available, we never touch Stripe.
        var offer = await getOfferQuery.Run(organization);
        if (offer is null)
        {
            return new BadRequest("Offer is no longer available.");
        }

        var assignment = await assignmentRepository.GetByOrganizationIdAsync(organization.Id);
        if (assignment is null)
        {
            return DefaultConflict;
        }

        var cohort = await cohortRepository.GetByIdAsync(assignment.CohortId);
        if (cohort is null || string.IsNullOrEmpty(cohort.ChurnDiscountCouponCode))
        {
            return DefaultConflict;
        }

        return cohort.MigrationPathId is not null
            ? await RedeemForMigrationCohortAsync(organization, assignment, cohort.ChurnDiscountCouponCode)
            : await RedeemForChurnOnlyCohortAsync(organization, assignment, cohort.ChurnDiscountCouponCode);
    });

    private async Task<BillingCommandResult<None>> RedeemForMigrationCohortAsync(
        Organization organization,
        Entities.OrganizationPlanMigrationCohortAssignment assignment,
        string churnDiscountCouponCode)
    {
        // Stripe-first, DB-write second. Set-union semantics make this branch self-healing
        // on retry: a re-attempt sees the coupon already on Phase 2 and no-ops the Stripe
        // call before writing ChurnDiscountAppliedDate.
        var subscription = await FetchSubscriptionAsync(organization);
        if (subscription is null)
        {
            return DefaultConflict;
        }

        var schedules = await stripeAdapter.ListSubscriptionSchedulesAsync(
            new SubscriptionScheduleListOptions { Customer = subscription.CustomerId });

        var activeSchedule = schedules.Data.FirstOrDefault(s =>
            s.Status == SubscriptionScheduleStatus.Active && s.SubscriptionId == subscription.Id);

        if (activeSchedule is not { Phases.Count: > 0 })
        {
            return DefaultConflict;
        }

        var now = subscription.TestClock?.FrozenTime ?? DateTime.UtcNow;
        var migrationPhases = activeSchedule.Phases.Where(p => p.EndDate > now).ToList();

        // Exactly 2 phases expected (Phase 1 active, Phase 2 future). PM-37170 diverges from
        // PM-37083's "proceed with first two" stance: this command is a financial mutation
        // in response to a save-offer modal -- silently dropping phase 3 from the rebuild
        // would be silently destructive (Stripe treats a missing phase as a delete intent).
        switch (migrationPhases.Count)
        {
            case 1:
                _logger.LogWarning(
                    "{Command}: Schedule ({ScheduleId}) for Organization ({OrganizationId}) has only 1 unexpired phase; webhook race likely advanced Phase 1->Phase 2",
                    CommandName, activeSchedule.Id, organization.Id);
                return DefaultConflict;
            case > 2:
                _logger.LogWarning(
                    "{Command}: Schedule ({ScheduleId}) for Organization ({OrganizationId}) has {PhaseCount} unexpired phases; expected 2",
                    CommandName, activeSchedule.Id, organization.Id, migrationPhases.Count);
                return DefaultConflict;
        }

        var phase1 = migrationPhases[0];
        var phase2 = migrationPhases[1];

        var phase2Discounts = phase2.Discounts?
            .Select(d => new SubscriptionSchedulePhaseDiscountOptions { Coupon = d.CouponId })
            .ToList() ?? [];

        // Set-union (not append): Stripe does not deduplicate identical coupons in a discounts
        // array -- appending a coupon already present produces either a double-discount stack
        // or an API error. Also makes the redeem path idempotent at the Stripe layer.
        var alreadyApplied = phase2Discounts.Any(d =>
            string.Equals(d.Coupon, churnDiscountCouponCode, StringComparison.Ordinal));

        if (!alreadyApplied)
        {
            phase2Discounts.Add(new SubscriptionSchedulePhaseDiscountOptions { Coupon = churnDiscountCouponCode });
        }

        var phases = new List<SubscriptionSchedulePhaseOptions>
        {
            // Phase 1 is current_phase with StartDate in the past. Any deviation from its
            // current state causes Stripe to reject the schedule update with "cannot modify
            // past phase." Mirror items, discounts, metadata, start/end, and proration
            // verbatim -- DO NOT edit Phase 1 fields here.
            BuildMirroredPhaseOptions(phase1),
            new()
            {
                StartDate = phase2.StartDate,
                EndDate = phase2.EndDate,
                Items = phase2.Items
                    .Select(i => new SubscriptionSchedulePhaseItemOptions { Price = i.PriceId, Quantity = i.Quantity })
                    .ToList(),
                Discounts = phase2Discounts,
                Metadata = phase2.Metadata,
                ProrationBehavior = phase2.ProrationBehavior
            }
        };

        // If the coupon is already on Phase 2 from a prior redeem, this entire flow is a no-op:
        // we skip the Stripe call (Stripe doesn't dedupe identical coupons in a discounts array),
        // and we preserve the existing ChurnDiscountAppliedDate -- overwriting it would lose the
        // original redeem timestamp (relevant for Marketing analytics).
        if (alreadyApplied)
        {
            _logger.LogInformation(
                "{Command}: Coupon already present on Phase 2 of schedule ({ScheduleId}) for Organization ({OrganizationId}); no Stripe update needed",
                CommandName, activeSchedule.Id, organization.Id);
            return new None();
        }

        try
        {
            // Customer.Discount is intentionally not mirrored into the phase Discounts list:
            // customer-level discounts are managed elsewhere and are preserved by Stripe
            // across schedule updates automatically.
            await stripeAdapter.UpdateSubscriptionScheduleAsync(activeSchedule.Id,
                new SubscriptionScheduleUpdateOptions
                {
                    EndBehavior = SubscriptionScheduleEndBehavior.Release,
                    Phases = phases
                });
        }
        catch (StripeException stripeException) when (IsCannotModifyPastPhase(stripeException))
        {
            _logger.LogWarning(stripeException,
                "{Command}: Stripe rejected schedule ({ScheduleId}) update for Organization ({OrganizationId}); subscription advanced past Phase 1 during redemption",
                CommandName, activeSchedule.Id, organization.Id);
            return new BadRequest("Your subscription has just renewed. Please refresh and try again.");
        }

        // ChurnDiscountAppliedDate is informational for migration cohorts (eligibility window
        // closes via Stripe's current_phase advance + MigratedDate from SubscriptionUpdatedHandler
        // when PM-37092 lands). If this write fails after the Stripe call succeeds, the set-union
        // semantics above make a retry a no-op -- harmless.
        assignment.ChurnDiscountAppliedDate = DateTime.UtcNow;
        assignment.RevisionDate = DateTime.UtcNow;
        await assignmentRepository.ReplaceAsync(assignment);

        _logger.LogInformation(
            "{Command}: Applied churn coupon to schedule ({ScheduleId}) Phase 2 for Organization ({OrganizationId}) Assignment ({AssignmentId}) Cohort ({CohortId}) Subscription ({SubscriptionId})",
            CommandName, activeSchedule.Id, organization.Id, assignment.Id, assignment.CohortId, subscription.Id);

        return new None();
    }

    private async Task<BillingCommandResult<None>> RedeemForChurnOnlyCohortAsync(
        Organization organization,
        Entities.OrganizationPlanMigrationCohortAssignment assignment,
        string churnDiscountCouponCode)
    {
        // CAS-first, Stripe-second. For once-duration coupons the per-assignment guard is the
        // ONLY post-consumption defense; Stripe-first opens a TOCTOU window where two parallel
        // POSTs both pass revalidation and both mutate Stripe before either DB write lands.
        // For repeating/forever the Stripe-side "coupon currently on subscription" check is
        // sufficient on its own -- we apply CAS-first to all three for consistency.
        var now = DateTime.UtcNow;
        var claimed = await assignmentRepository.TryClaimChurnDiscountAsync(assignment.Id, now);
        if (!claimed)
        {
            _logger.LogInformation(
                "{Command}: CAS lost race for Organization ({OrganizationId}) Assignment ({AssignmentId}) Cohort ({CohortId}); assignment already claimed",
                CommandName, organization.Id, assignment.Id, assignment.CohortId);
            return new BadRequest("Offer is no longer available.");
        }

        var subscription = await FetchSubscriptionAsync(organization);
        if (subscription is null)
        {
            _logger.LogError(
                "{Command}: CAS claimed Assignment ({AssignmentId}) but subscription ({SubscriptionId}) for Organization ({OrganizationId}) was not found; reconciliation required",
                CommandName, assignment.Id, organization.GatewaySubscriptionId, organization.Id);
            return DefaultConflict;
        }

        var existingDiscounts = subscription.Discounts?
            .Select(d => new SubscriptionDiscountOptions { Coupon = d.Coupon.Id })
            .ToList() ?? [];

        var alreadyApplied = existingDiscounts.Any(d =>
            string.Equals(d.Coupon, churnDiscountCouponCode, StringComparison.Ordinal));

        if (!alreadyApplied)
        {
            existingDiscounts.Add(new SubscriptionDiscountOptions { Coupon = churnDiscountCouponCode });

            // Customer.Discount is intentionally NOT mutated here. It's managed elsewhere
            // (manual ops adjustments, audience filters via SubscriptionDiscountService).
            // The churn-only code path only writes to subscription.discounts.
            await stripeAdapter.UpdateSubscriptionAsync(subscription.Id,
                new SubscriptionUpdateOptions { Discounts = existingDiscounts });
        }

        _logger.LogInformation(
            "{Command}: Applied churn coupon to Subscription ({SubscriptionId}) for Organization ({OrganizationId}) Assignment ({AssignmentId}) Cohort ({CohortId})",
            CommandName, subscription.Id, organization.Id, assignment.Id, assignment.CohortId);

        return new None();
    }

    private static SubscriptionSchedulePhaseOptions BuildMirroredPhaseOptions(SubscriptionSchedulePhase phase) =>
        new()
        {
            StartDate = phase.StartDate,
            EndDate = phase.EndDate,
            Items = phase.Items
                .Select(i => new SubscriptionSchedulePhaseItemOptions { Price = i.PriceId, Quantity = i.Quantity })
                .ToList(),
            Discounts = phase.Discounts?
                .Select(d => new SubscriptionSchedulePhaseDiscountOptions { Coupon = d.CouponId })
                .ToList(),
            Metadata = phase.Metadata,
            ProrationBehavior = phase.ProrationBehavior
        };

    private async Task<Subscription?> FetchSubscriptionAsync(Organization organization)
    {
        try
        {
            return await stripeAdapter.GetSubscriptionAsync(organization.GatewaySubscriptionId,
                new SubscriptionGetOptions
                {
                    Expand = ["customer", "test_clock", "discounts.coupon"]
                });
        }
        catch (StripeException stripeException) when (stripeException.StripeError?.Code == ErrorCodes.ResourceMissing)
        {
            _logger.LogError(
                "{Command}: Subscription ({SubscriptionId}) for Organization ({OrganizationId}) was not found",
                CommandName, organization.GatewaySubscriptionId, organization.Id);
            return null;
        }
    }

    private static bool IsCannotModifyPastPhase(StripeException stripeException) =>
        stripeException.StripeError?.Message is { Length: > 0 } message
        && _cannotModifyPastPhaseHints.Any(hint =>
            message.Contains(hint, StringComparison.OrdinalIgnoreCase));
}
