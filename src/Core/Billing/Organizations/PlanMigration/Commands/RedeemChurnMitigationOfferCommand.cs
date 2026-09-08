using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Commands;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Extensions;
using Bit.Core.Billing.Organizations.Helpers;
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
        var subscription = await OrganizationSubscriptionHelpers.TryGetSubscriptionAsync(
            stripeAdapter, _logger, organization,
            ["customer.discount.source.coupon", "test_clock", "discounts.source.coupon"]);
        if (subscription is null)
        {
            return DefaultConflict;
        }
        DiscountExtensions.RequireScheduleDiscountExpansions(subscription, _logger);

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

        var currentPhase2CouponIds = phase2.Discounts?.Select(d => d.CouponId).ToList() ?? [];
        var phase2Discounts = DiscountExtensions.BuildPhaseLevelDiscounts(
            subscription,
            [churnDiscountCouponCode],
            preservedCouponIds: currentPhase2CouponIds);

        // No-op only when the rebuild's coupon-level footprint adds nothing to Phase 2's current
        // coupons. Comparing coupon ids -- rather than the Discount/Coupon write-representation
        // split BuildPhaseLevelDiscounts produces -- keeps the check representation-independent: a
        // subscription discount carried forward as `Discount = di_...` resolves back to the same
        // coupon id Phase 2 already shows as `Coupon = ...`.
        var currentPhase2CouponFootprint = BuildCouponFootprint(currentPhase2CouponIds);
        var rebuiltPhase2CouponFootprint = BuildCouponFootprint(
            currentPhase2CouponIds,
            [subscription.Customer?.Discount?.Source?.CouponId],
            subscription.Discounts?.Select(d => d.Source?.CouponId) ?? [],
            [churnDiscountCouponCode]);

        if (rebuiltPhase2CouponFootprint.SetEquals(currentPhase2CouponFootprint))
        {
            _logger.LogInformation(
                "{Command}: Discounts already present on Phase 2 of schedule ({ScheduleId}) for Organization ({OrganizationId}); no Stripe update needed",
                CommandName, activeSchedule.Id, organization.Id);
            return new None();
        }

        var phases = new List<SubscriptionSchedulePhaseOptions>
        {
            // Phase 1 is current_phase with StartDate in the past; mirror its items, metadata,
            // start/end, and proration verbatim. Phase-level discounts carry only what is still live
            // on the subscription (by discount id) so a one-time coupon already consumed on the
            // current invoice -- still recorded on the phase but gone from subscription.Discounts --
            // isn't re-minted on the wholesale replace.
            BuildMirroredPhaseOptions(phase1, subscription),
            new()
            {
                StartDate = phase2.StartDate,
                EndDate = phase2.EndDate,
                Items = phase2.Items.Select(i => new SubscriptionSchedulePhaseItemOptions
                {
                    Price = i.PriceId,
                    Quantity = i.Quantity,
                    Discounts = DiscountExtensions.BuildPhaseItemLevelDiscounts(i.Discounts?.Select(d => d.CouponId) ?? [])
                }).ToList(),
                Discounts = phase2Discounts,
                Metadata = phase2.Metadata,
                ProrationBehavior = phase2.ProrationBehavior
            }
        };

        await stripeAdapter.UpdateSubscriptionScheduleAsync(activeSchedule.Id,
            new SubscriptionScheduleUpdateOptions
            {
                EndBehavior = SubscriptionScheduleEndBehavior.Release,
                Phases = phases
            });

        // ChurnDiscountAppliedDate is informational for migration cohorts (eligibility window
        // closes via Stripe's current_phase advance + MigratedDate from SubscriptionUpdatedHandler
        // when PM-37092 lands). If this write fails after the Stripe call succeeds, the merged-set
        // no-op guard above makes a retry a no-op -- harmless.
        var nowUtc = DateTime.UtcNow;
        assignment.ChurnDiscountAppliedDate = nowUtc;
        assignment.RevisionDate = nowUtc;
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
        var subscription = await OrganizationSubscriptionHelpers.TryGetSubscriptionAsync(
            stripeAdapter, _logger, organization,
            ["customer.discount.source.coupon", "test_clock", "discounts.source.coupon"]);
        if (subscription is null)
        {
            return DefaultConflict;
        }
        DiscountExtensions.RequireScheduleDiscountExpansions(subscription, _logger);

        var currentCouponIds = subscription.Discounts?
            .Select(d => d.Source?.Coupon?.Id)
            .Where(id => !string.IsNullOrEmpty(id))
            .ToList() ?? [];

        // A discount with no resolvable coupon (deleted in Stripe, or "discounts.source.coupon" not
        // expanded) is still carried forward by BuildSubscriptionLevelDiscounts below, referenced by
        // its discount id -- dropping it here would delete a live discount from Stripe on the
        // wholesale replace. Log it purely as a diagnostic: an unresolved coupon usually means
        // incomplete expansion, or the coupon was deleted in Stripe.
        var unresolvableDiscountCount = subscription.Discounts?.Count(d => string.IsNullOrEmpty(d?.Source?.Coupon?.Id)) ?? 0;
        if (unresolvableDiscountCount > 0)
        {
            _logger.LogWarning(
                "{Command}: {Count} discount(s) on Subscription ({SubscriptionId}) for Organization ({OrganizationId}) had no resolvable coupon; carried forward by discount id, but confirm 'discounts.source.coupon' is expanded",
                CommandName, unresolvableDiscountCount, subscription.Id, organization.Id);
        }

        var subscriptionDiscounts = DiscountExtensions.BuildSubscriptionLevelDiscounts(
            subscription, [churnDiscountCouponCode]);

        // No-op only when the rebuild's coupon-level footprint adds nothing to the subscription's
        // current discounts -- see the Phase 2 comment above for why this compares coupon ids
        // rather than the Discount/Coupon write-representation BuildSubscriptionLevelDiscounts produces.
        var currentCouponFootprint = BuildCouponFootprint(currentCouponIds);
        var rebuiltCouponFootprint = BuildCouponFootprint(
            currentCouponIds,
            [subscription.Customer?.Discount?.Source?.CouponId],
            [churnDiscountCouponCode]);

        if (rebuiltCouponFootprint.SetEquals(currentCouponFootprint))
        {
            _logger.LogInformation(
                "{Command}: Discounts already present on Subscription ({SubscriptionId}) for Organization ({OrganizationId}); no Stripe update needed",
                CommandName, subscription.Id, organization.Id);
            return new None();
        }

        // Stamp the per-assignment one-shot guard BEFORE mutating Stripe. For a `once`-duration
        // coupon this is the only post-consumption defense against double-application: if Stripe
        // succeeds, finalizes the next invoice, and consumes the coupon, a retry from the UI must
        // not re-evaluate as eligible.
        var nowUtc = DateTime.UtcNow;
        assignment.ChurnDiscountAppliedDate = nowUtc;
        assignment.RevisionDate = nowUtc;
        await assignmentRepository.ReplaceAsync(assignment);

        try
        {
            await stripeAdapter.UpdateSubscriptionAsync(subscription.Id,
                new SubscriptionUpdateOptions { Discounts = subscriptionDiscounts });
        }
        catch
        {
            // Best-effort rollback so a Stripe failure doesn't permanently lock the org out of
            // a UI retry. If the rollback itself fails, ops clears ChurnDiscountAppliedDate
            // manually -- a documented recovery surface that's strictly better than the
            // alternative (silent double-application after Stripe consumes the coupon).
            assignment.ChurnDiscountAppliedDate = null;
            assignment.RevisionDate = DateTime.UtcNow;
            try
            {
                await assignmentRepository.ReplaceAsync(assignment);
            }
            catch (Exception rollbackEx)
            {
                _logger.LogError(rollbackEx,
                    "{Command}: Rollback of ChurnDiscountAppliedDate failed on Assignment ({AssignmentId}) for Organization ({OrganizationId}); manual clear required",
                    CommandName, assignment.Id, organization.Id);
            }
            throw;
        }

        _logger.LogInformation(
            "{Command}: Applied churn coupon to Subscription ({SubscriptionId}) for Organization ({OrganizationId}) Assignment ({AssignmentId}) Cohort ({CohortId})",
            CommandName, subscription.Id, organization.Id, assignment.Id, assignment.CohortId);

        return new None();
    }

    private static SubscriptionSchedulePhaseOptions BuildMirroredPhaseOptions(
        SubscriptionSchedulePhase phase, Subscription subscription) =>
        new()
        {
            StartDate = phase.StartDate,
            EndDate = phase.EndDate,
            Items = phase.Items
                .Select(i => new SubscriptionSchedulePhaseItemOptions
                {
                    Price = i.PriceId,
                    Quantity = i.Quantity,
                    Discounts = DiscountExtensions.BuildPhaseItemLevelDiscounts(i.Discounts?.Select(d => d.CouponId) ?? [])
                })
                .ToList(),
            Discounts = DiscountExtensions.BuildCurrentPhaseDiscounts(subscription),
            Metadata = phase.Metadata,
            ProrationBehavior = phase.ProrationBehavior
        };

    // Builds the set of coupon ids represented across the given sources, skipping null/empty
    // entries. Used to compare a rebuild's would-be discount set against the current one at the
    // coupon level, independent of whether either side records a discount by coupon or discount id.
    private static HashSet<string> BuildCouponFootprint(params IEnumerable<string?>[] couponIdSources)
    {
        var footprint = new HashSet<string>(StringComparer.Ordinal);
        foreach (var couponId in couponIdSources.SelectMany(source => source)
                     .Where(couponId => !string.IsNullOrEmpty(couponId)))
        {
            footprint.Add(couponId!);
        }
        return footprint;
    }
}
