using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Commands;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Extensions;
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
        var subscription = await TryGetSubscriptionAsync(organization);
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

        var currentPhase2CouponIds = phase2.Discounts?.Select(d => d.CouponId).ToList() ?? [];
        var mergedPhase2CouponIds = (subscription.Customer?.Discount).MergeDiscountCouponIds(
            currentPhase2CouponIds,
            churnDiscountCouponCode);

        // No-op only when the merged set equals Phase 2's current discounts — comparing the merged
        // set (not just the churn coupon) lets an already-redeemed, still-shadowed org self-heal on retry.
        if (mergedPhase2CouponIds.SequenceEqual(currentPhase2CouponIds, StringComparer.Ordinal))
        {
            _logger.LogInformation(
                "{Command}: Discounts already present on Phase 2 of schedule ({ScheduleId}) for Organization ({OrganizationId}); no Stripe update needed",
                CommandName, activeSchedule.Id, organization.Id);
            return new None();
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
                Discounts = mergedPhase2CouponIds.ToPhaseDiscountOptions(),
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
        var subscription = await TryGetSubscriptionAsync(organization);
        if (subscription is null)
        {
            return DefaultConflict;
        }

        var currentCouponIds = subscription.Discounts?.Select(d => d.Coupon.Id).ToList() ?? [];
        var mergedCouponIds = (subscription.Customer?.Discount).MergeDiscountCouponIds(
            currentCouponIds,
            churnDiscountCouponCode);

        // No-op only when the merged set equals the current subscription discounts — comparing the
        // merged set (not just the churn coupon) lets an already-redeemed, still-shadowed org self-heal.
        if (mergedCouponIds.SequenceEqual(currentCouponIds, StringComparer.Ordinal))
        {
            _logger.LogInformation(
                "{Command}: Discounts already present on Subscription ({SubscriptionId}) for Organization ({OrganizationId}); no Stripe update needed",
                CommandName, subscription.Id, organization.Id);
            return new None();
        }

        var existingDiscounts = mergedCouponIds.ToSubscriptionDiscountOptions();

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
                new SubscriptionUpdateOptions { Discounts = existingDiscounts });
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

    private async Task<Subscription?> TryGetSubscriptionAsync(Organization organization)
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

}
