using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Commands;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Extensions;
using Bit.Core.Billing.Organizations.AnnualUpgradeOffer;
using Bit.Core.Billing.Organizations.Helpers;
using Bit.Core.Billing.Organizations.Models;
using Bit.Core.Billing.Organizations.PlanMigration.Repositories;
using Bit.Core.Billing.Organizations.PlanMigration.ValueObjects;
using Bit.Core.Billing.Organizations.Schedules;
using Bit.Core.Billing.Organizations.Schedules.Enums;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Services;
using Microsoft.Extensions.Logging;
using OneOf;
using Stripe;
using Plan = Bit.Core.Models.StaticStore.Plan;

namespace Bit.Core.Billing.Organizations.Commands;

using static StripeConstants;

/// <summary>
/// Updates an organization's Stripe subscription based on a set of changes described by an
/// <see cref="OrganizationSubscriptionChangeSet"/>. Handles adding, removing, and updating
/// subscription items as well as proration, invoice finalization, and tax exemption reconciliation.
/// </summary>
public interface IUpdateOrganizationSubscriptionCommand
{
    /// <summary>
    /// Applies the provided <paramref name="changeSet"/> to the organization's Stripe subscription.
    /// </summary>
    /// <param name="organization">The organization whose subscription will be updated.</param>
    /// <param name="changeSet">The set of changes to apply to the subscription.</param>
    /// <param name="subscription">
    /// An optional pre-fetched subscription. When supplied and it carries the required expansions
    /// (an expanded <see cref="Subscription.Customer"/> and, if a schedule is attached, an expanded
    /// <see cref="Subscription.Schedule"/>), it is reused to avoid a redundant Stripe call;
    /// otherwise the subscription is re-fetched.
    /// </param>
    /// <returns>
    /// A <see cref="BillingCommandResult{T}"/> containing the updated <see cref="Subscription"/>
    /// on success, or an error result if validation or the Stripe operation fails.
    /// </returns>
    Task<BillingCommandResult<Subscription>> Run(
        Organization organization,
        OrganizationSubscriptionChangeSet changeSet,
        Subscription? subscription = null);
}

public class UpdateOrganizationSubscriptionCommand(
    ILogger<UpdateOrganizationSubscriptionCommand> logger,
    IOrganizationPlanMigrationCohortAssignmentRepository assignmentRepository,
    IOrganizationPlanMigrationCohortRepository cohortRepository,
    IPricingClient pricingClient,
    IStripeAdapter stripeAdapter) : BaseBillingCommand<UpdateOrganizationSubscriptionCommand>(logger), IUpdateOrganizationSubscriptionCommand
{
    private static readonly List<string> _validSubscriptionStatusesForUpdate =
    [
        SubscriptionStatus.Trialing,
        SubscriptionStatus.Active,
        SubscriptionStatus.PastDue
    ];

    private readonly ILogger<UpdateOrganizationSubscriptionCommand> _logger = logger;

    protected override Conflict DefaultConflict =>
        new("We had a problem updating your subscription. Please contact support for assistance.");

    public Task<BillingCommandResult<Subscription>> Run(
        Organization organization,
        OrganizationSubscriptionChangeSet changeSet,
        Subscription? subscription = null) => HandleAsync<Subscription>(async () =>
    {
        subscription = HasRequiredExpansions(subscription)
            ? subscription
            : await OrganizationSubscriptionHelpers.TryGetSubscriptionAsync(
                stripeAdapter, _logger, organization,
                ["customer", "customer.discount.source.coupon", "test_clock", "schedule", "discounts.source.coupon"]);

        if (subscription is null)
        {
            return new BadRequest("We couldn't find your subscription.");
        }

        if (!_validSubscriptionStatusesForUpdate.Contains(subscription.Status))
        {
            _logger.LogWarning(
                "{Command}: Tried to update organization ({OrganizationId}) subscription ({SubscriptionId}) with status ({SubscriptionStatus})",
                CommandName, organization.Id, subscription.Id, subscription.Status);
            return new BadRequest("Your subscription cannot be updated in its current status.");
        }

        if (changeSet.Changes.Count == 0)
        {
            _logger.LogWarning(
                "{Command}: Change set for organization ({OrganizationId}) subscription ({SubscriptionId}) contained zero changes",
                CommandName, organization.Id, subscription.Id);
            return new Conflict("No changes were provided for the organization subscription update");
        }

        var hasStructuralChanges = changeSet.ChargeImmediately;
        var isChargedAutomatically = subscription.CollectionMethod == CollectionMethod.ChargeAutomatically;
        var isBilledAnnually = subscription.Items.FirstOrDefault()?.Price.Recurring?.Interval == Intervals.Year;

        var prorationBehavior =
            hasStructuralChanges ? ProrationBehavior.AlwaysInvoice : ProrationBehavior.CreateProrations;
        var paymentBehavior =
            hasStructuralChanges && isChargedAutomatically ? PaymentBehavior.PendingIfIncomplete : null;

        var items = new List<SubscriptionItemOptions>();
        foreach (var change in changeSet.Changes)
        {
            var validationResult = change.Match(
                addItem => ValidateItemAddition(addItem, subscription),
                changeItemPrice => ValidateItemPriceChange(changeItemPrice, subscription),
                removeItem => ValidateItemRemoval(removeItem, subscription),
                updateItemQuantity => ValidateItemQuantityUpdate(updateItemQuantity, subscription));

            if (validationResult.IsT1)
            {
                return validationResult.AsT1;
            }

            items.Add(validationResult.AsT0);
        }

        var activeSchedule = subscription.Schedule is { Status: SubscriptionScheduleStatus.Active } attached
            ? attached
            : null;

        if (activeSchedule is { Phases.Count: > 0 })
        {
            // PM-40537: only rewrite schedules our code created, identified by phase metadata.
            var annualUpgradePlans = await ResolveAnnualUpgradePhasePlansAsync(organization, subscription);
            var schedulePlans = annualUpgradePlans
                                ?? await ResolveCohortMigrationPhasePlansAsync(organization, subscription);
            if (schedulePlans is { } plans)
            {
                var now = subscription.TestClock?.FrozenTime ?? DateTime.UtcNow;

                // Stripe normalizes attached schedules into 3 phases when the subscription is mutated:
                // an anchor phase covering current_period_start -> schedule.created becomes phases[0].
                // Strict > on EndDate: a phase ending exactly at `now` has effectively ended, and Stripe
                // rejects schedule updates that include past phases.
                var migrationPhases = activeSchedule.Phases.Where(p => p.EndDate > now).ToList();

                if (migrationPhases.Count == 0)
                {
                    _logger.LogWarning(
                        "{Command}: Schedule ({ScheduleId}) has no updatable phases remaining",
                        CommandName, activeSchedule.Id);
                    return DefaultConflict;
                }

                if (migrationPhases.Count > 2)
                {
                    _logger.LogWarning(
                        "{Command}: Schedule ({ScheduleId}) has {PhaseCount} active phases — expected at most 2. Only the first two will be updated.",
                        CommandName, activeSchedule.Id, migrationPhases.Count);
                }

                _logger.LogInformation(
                    "{Command}: Active migration schedule ({ScheduleId}) found for subscription ({SubscriptionId}), updating {PhaseCount} active phase(s)",
                    CommandName, activeSchedule.Id, subscription.Id, migrationPhases.Count);

                // Annual upgrade reuses existing discounts and adds no coupon.
                var phases = annualUpgradePlans is not null
                    ? AnnualUpgradeSchedulePhaseRebuilder.BuildUpdatedPhases(
                        migrationPhases, changeSet.Changes, plans.source, plans.target)
                    : BuildUpdatedPhases(migrationPhases, changeSet.Changes,
                        plans.source, plans.target, subscription);

                await stripeAdapter.UpdateSubscriptionScheduleAsync(activeSchedule.Id,
                    new SubscriptionScheduleUpdateOptions
                    {
                        EndBehavior = SubscriptionScheduleEndBehavior.Release,
                        Phases = phases,
                        ProrationBehavior = prorationBehavior
                    });

                return subscription;
            }

            _logger.LogInformation(
                "{Command}: Active schedule ({ScheduleId}) on subscription ({SubscriptionId}) is one our code did not create; leaving it untouched and updating the subscription directly",
                CommandName, activeSchedule.Id, subscription.Id);
        }

        var options = new SubscriptionUpdateOptions { Items = items, ProrationBehavior = prorationBehavior };

        if (paymentBehavior is not null)
        {
            options.PaymentBehavior = paymentBehavior;
        }

        if (isBilledAnnually && !hasStructuralChanges && subscription.Status != SubscriptionStatus.Trialing)
        {
            options.PendingInvoiceItemInterval = new SubscriptionPendingInvoiceItemIntervalOptions
            {
                Interval = Intervals.Month
            };
        }

        var updatedSubscription = await stripeAdapter.UpdateSubscriptionAsync(subscription.Id, options);

        // ReSharper disable once InvertIf
        if (!isChargedAutomatically && hasStructuralChanges && updatedSubscription.LatestInvoiceId is not null)
        {
            var invoice = await stripeAdapter.GetInvoiceAsync(updatedSubscription.LatestInvoiceId);

            if (invoice is { Status: InvoiceStatus.Draft })
            {
                var finalizedInvoice = await stripeAdapter.FinalizeInvoiceAsync(invoice.Id,
                    new InvoiceFinalizeOptions { AutoAdvance = false });

                await stripeAdapter.SendInvoiceAsync(finalizedInvoice.Id);
            }
            else
            {
                _logger.LogWarning(
                    "{Command}: Latest invoice ({InvoiceId}) after subscription ({SubscriptionId}) update for organization ({OrganizationId}) was in '{Status}' status",
                    CommandName, invoice.Id, subscription.Id, organization.Id, invoice.Status);
            }
        }

        return updatedSubscription;
    });

    // Reused subscriptions must carry Customer for tax reconciliation, the attached schedule (which
    // ownership classification reads), a fully-expanded discounts list, and an expanded test clock —
    // the same expansions BuildPhaseOptions' discount builders rely on. A mis-expanded subscription
    // fails this check and gets re-fetched instead of reaching the discount builders unexpanded.
    private static bool HasRequiredExpansions(Subscription? subscription) =>
        subscription is { Customer: not null } &&
        (string.IsNullOrEmpty(subscription.ScheduleId) || subscription.Schedule is not null) &&
        !(subscription.Discounts is { Count: > 0 } && subscription.Discounts.Any(d => d is null)) &&
        (subscription.TestClockId is null || subscription.TestClock is not null);

    // An annual-upgrade schedule (PM-38333) is recognised by the marker redemption stamps on its
    // phases. When recognised, source is the current monthly plan and target is the annual-latest
    // plan, so phase 1 stays monthly (identity) and phase 2 maps to annual-latest. Returns null
    // when this is not an annual-upgrade schedule, letting the caller fall back to cohort-migration
    // resolution.
    private async Task<(Plan source, Plan target)?> ResolveAnnualUpgradePhasePlansAsync(
        Organization organization, Subscription subscription)
    {
        if (SubscriptionScheduleOwnershipMapper.Map(subscription) !=
            OrganizationSubscriptionScheduleOwnership.AnnualUpgrade)
        {
            return null;
        }

        var annualLatestPlanType = AnnualUpgradeOfferPlans.ResolveAnnualLatestPlanType(organization.PlanType);
        if (annualLatestPlanType is null)
        {
            return null;
        }

        var currentPlan = await pricingClient.GetPlanOrThrow(organization.PlanType);
        var annualLatestPlan = await pricingClient.GetPlanOrThrow(annualLatestPlanType.Value);
        return (currentPlan, annualLatestPlan);
    }

    private async Task<(Plan source, Plan target)?> ResolveCohortMigrationPhasePlansAsync(
        Organization organization, Subscription subscription)
    {
        if (SubscriptionScheduleOwnershipMapper.Map(subscription) !=
            OrganizationSubscriptionScheduleOwnership.PriceMigration)
        {
            return null;
        }

        var migrationPath = await TryResolveMigrationPathAsync(organization.Id);
        if (migrationPath is null)
        {
            return null;
        }

        var source = await pricingClient.GetPlanOrThrow(migrationPath.FromPlan);
        var target = await pricingClient.GetPlanOrThrow(migrationPath.ToPlan);
        return (source, target);
    }

    private async Task<MigrationPath?> TryResolveMigrationPathAsync(Guid organizationId)
    {
        var assignment = await assignmentRepository.GetByOrganizationIdAsync(organizationId);
        if (assignment is null)
        {
            return null;
        }

        var cohort = await cohortRepository.GetByIdAsync(assignment.CohortId);
        if (cohort?.MigrationPathId is null)
        {
            return null;
        }

        return MigrationPaths.FromId(cohort.MigrationPathId.Value);
    }

    private static OneOf<SubscriptionItemOptions, BadRequest> ValidateItemAddition(
        AddItem addItem, Subscription subscription)
    {
        var duplicate = subscription.Items.Data
            .FirstOrDefault(i => i.Price.Id == addItem.PriceId);

        if (duplicate is not null)
        {
            return new BadRequest($"Subscription already contains an item with price '{addItem.PriceId}'.");
        }

        return new SubscriptionItemOptions
        {
            Price = addItem.PriceId,
            Quantity = addItem.Quantity
        };
    }

    private static OneOf<SubscriptionItemOptions, BadRequest> ValidateItemPriceChange(
        ChangeItemPrice priceChange, Subscription subscription)
    {
        var currentItem = subscription.Items.Data
            .FirstOrDefault(i => i.Price.Id == priceChange.CurrentPriceId);

        if (currentItem is null)
        {
            return new BadRequest($"Subscription does not contain an item with price '{priceChange.CurrentPriceId}'.");
        }

        return new SubscriptionItemOptions
        {
            Id = currentItem.Id,
            Price = priceChange.UpdatedPriceId,
            Quantity = priceChange.Quantity ?? currentItem.Quantity
        };
    }

    private static OneOf<SubscriptionItemOptions, BadRequest> ValidateItemQuantityUpdate(
        UpdateItemQuantity updateItemQuantity, Subscription subscription)
    {
        var existingItem = subscription.Items.Data
            .FirstOrDefault(i => i.Price.Id == updateItemQuantity.PriceId);

        if (existingItem is null)
        {
            return new BadRequest($"Subscription does not contain an item with price '{updateItemQuantity.PriceId}'.");
        }

        return updateItemQuantity.Quantity == 0
            ? new SubscriptionItemOptions { Id = existingItem.Id, Deleted = true }
            : new SubscriptionItemOptions { Id = existingItem.Id, Price = updateItemQuantity.PriceId, Quantity = updateItemQuantity.Quantity };
    }

    private static OneOf<SubscriptionItemOptions, BadRequest> ValidateItemRemoval(
        RemoveItem removeItem, Subscription subscription)
    {
        var existingItem = subscription.Items.Data
            .FirstOrDefault(i => i.Price.Id == removeItem.PriceId);

        if (existingItem is null)
        {
            return new BadRequest($"Subscription does not contain an item with price '{removeItem.PriceId}'.");
        }

        return new SubscriptionItemOptions
        {
            Id = existingItem.Id,
            Deleted = true
        };
    }

    private static List<SubscriptionSchedulePhaseOptions> BuildUpdatedPhases(
        List<SubscriptionSchedulePhase> migrationPhases,
        IReadOnlyList<OrganizationSubscriptionChange> changes,
        Plan sourcePlan,
        Plan targetPlan,
        Subscription subscription)
    {
        var phase1IsPostMigration = migrationPhases.Count == 1
            && SchedulePhaseMapper.PhaseUsesTargetPlanPrices(migrationPhases[0], targetPlan);

        var phases = new List<SubscriptionSchedulePhaseOptions>();

        var phase1 = migrationPhases[0];
        phases.Add(BuildPhaseOptions(
            phase1, changes,
            source: sourcePlan,
            target: phase1IsPostMigration ? targetPlan : sourcePlan,
            subscription: subscription,
            isFuture: false));

        if (migrationPhases.Count >= 2)
        {
            phases.Add(BuildPhaseOptions(
                migrationPhases[1], changes,
                source: sourcePlan,
                target: targetPlan,
                subscription: subscription,
                isFuture: true));
        }

        return phases;
    }

    private static SubscriptionSchedulePhaseOptions BuildPhaseOptions(
        SubscriptionSchedulePhase sourcePhase,
        IReadOnlyList<OrganizationSubscriptionChange> changes,
        Plan source,
        Plan target,
        Subscription subscription,
        bool isFuture) =>
        new()
        {
            StartDate = sourcePhase.StartDate,
            EndDate = sourcePhase.EndDate,
            Items = SchedulePhaseMapper.ApplyChangesToPhaseItems(sourcePhase.Items, changes, source, target),
            Discounts = isFuture
                ? DiscountExtensions.BuildPhaseLevelDiscounts(
                    subscription, [], preservedCouponIds: sourcePhase.Discounts?.Select(d => d.CouponId))
                : DiscountExtensions.BuildCurrentPhaseDiscounts(subscription),
            Metadata = sourcePhase.Metadata,
            ProrationBehavior = sourcePhase.ProrationBehavior
        };
}
