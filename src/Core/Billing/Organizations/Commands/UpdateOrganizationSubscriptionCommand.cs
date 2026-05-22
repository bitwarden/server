using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Commands;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Organizations.Models;
using Bit.Core.Billing.Organizations.PlanMigration;
using Bit.Core.Billing.Organizations.PlanMigration.Repositories;
using Bit.Core.Billing.Organizations.PlanMigration.ValueObjects;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Services;
using Bit.Core.Billing.Tax.Utilities;
using Bit.Core.Services;
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
    /// <returns>
    /// A <see cref="BillingCommandResult{T}"/> containing the updated <see cref="Subscription"/>
    /// on success, or an error result if validation or the Stripe operation fails.
    /// </returns>
    Task<BillingCommandResult<Subscription>> Run(
        Organization organization,
        OrganizationSubscriptionChangeSet changeSet);
}

public class UpdateOrganizationSubscriptionCommand(
    IFeatureService featureService,
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
        OrganizationSubscriptionChangeSet changeSet) => HandleAsync<Subscription>(async () =>
    {
        var subscription = await FetchSubscriptionAsync(organization);

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

        if (!featureService.IsEnabled(FeatureFlagKeys.PM37597_AlwaysEnableStripeAutomaticTax))
        {
            await ReconcileTaxExemptionAsync(subscription.Customer);
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

        var schedules = await stripeAdapter.ListSubscriptionSchedulesAsync(
            new SubscriptionScheduleListOptions { Customer = subscription.CustomerId });
        var activeSchedule = schedules.Data.FirstOrDefault(s =>
            s.Status == SubscriptionScheduleStatus.Active && s.SubscriptionId == subscription.Id);

        if (activeSchedule is { Phases.Count: > 0 })
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
                "{Command}: Active subscription schedule ({ScheduleId}) found for subscription ({SubscriptionId}), updating {PhaseCount} active phase(s)",
                CommandName, activeSchedule.Id, subscription.Id, migrationPhases.Count);

            var (sourcePlan, targetPlan) = await ResolvePhasePlansAsync(organization);
            var phases = BuildUpdatedPhases(migrationPhases, changeSet.Changes, sourcePlan, targetPlan);

            await stripeAdapter.UpdateSubscriptionScheduleAsync(activeSchedule.Id,
                new SubscriptionScheduleUpdateOptions
                {
                    EndBehavior = SubscriptionScheduleEndBehavior.Release,
                    Phases = phases,
                    ProrationBehavior = prorationBehavior
                });

            return subscription;
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

    private async Task<Subscription?> FetchSubscriptionAsync(Organization organization)
    {
        try
        {
            return await stripeAdapter.GetSubscriptionAsync(organization.GatewaySubscriptionId, new SubscriptionGetOptions
            {
                Expand = ["customer", "test_clock"]
            });
        }
        catch (StripeException stripeException) when (stripeException.StripeError?.Code == ErrorCodes.ResourceMissing)
        {
            _logger.LogError("{Command}: Subscription ({SubscriptionId}) for Organization ({OrganizationId}) was not found",
                CommandName, organization.GatewaySubscriptionId, organization.Id);
            return null;
        }
    }

    private async Task<(Plan source, Plan target)> ResolvePhasePlansAsync(Organization organization)
    {
        var migrationPath = await TryResolveMigrationPathAsync(organization.Id);
        if (migrationPath is null)
        {
            var current = await pricingClient.GetPlanOrThrow(organization.PlanType);
            return (current, current);
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

    private async Task ReconcileTaxExemptionAsync(Customer customer)
    {
        var determinedTaxExemptStatus = TaxHelpers.DetermineTaxExemptStatus(customer.Address?.Country, customer.TaxExempt);
        switch (customer)
        {
            case { Address.Country: not null and not "", TaxExempt: var customerTaxExemptStatus }
                when determinedTaxExemptStatus != customerTaxExemptStatus:
                await stripeAdapter.UpdateCustomerAsync(customer.Id,
                    new CustomerUpdateOptions { TaxExempt = determinedTaxExemptStatus });
                break;
        }

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
        Plan targetPlan)
    {
        var phase1IsPostMigration = migrationPhases.Count == 1
            && IsPostMigrationPhase(migrationPhases[0], sourcePlan, targetPlan);

        var phases = new List<SubscriptionSchedulePhaseOptions>();

        var phase1 = migrationPhases[0];
        phases.Add(BuildPhaseOptions(
            phase1, changes,
            source: sourcePlan,
            target: phase1IsPostMigration ? targetPlan : sourcePlan,
            suppressDiscounts: phase1IsPostMigration));

        if (migrationPhases.Count >= 2)
        {
            phases.Add(BuildPhaseOptions(
                migrationPhases[1], changes,
                source: sourcePlan,
                target: targetPlan,
                suppressDiscounts: false));
        }

        return phases;
    }

    // For non-migrations (source == target), a lone remaining phase always means Stripe has rolled
    // past phase 1. For migrations, require the phase to actually use target-plan price IDs — a
    // legacy source-priced single-phase schedule (cancelled without releasing) would otherwise have
    // its still-valid migration discount wrongly suppressed.
    private static bool IsPostMigrationPhase(SubscriptionSchedulePhase phase, Plan source, Plan target)
    {
        if (ReferenceEquals(source, target))
        {
            return true;
        }

        var targetIds = new HashSet<string>(StringComparer.Ordinal)
        {
            target.PasswordManager.StripeSeatPlanId,
            target.PasswordManager.StripeStoragePlanId
        };
        if (target.SecretsManager?.StripeSeatPlanId is { } smSeat)
        {
            targetIds.Add(smSeat);
        }
        if (target.SecretsManager?.StripeServiceAccountPlanId is { } smServiceAccount)
        {
            targetIds.Add(smServiceAccount);
        }

        return phase.Items.Any(item => targetIds.Contains(item.PriceId));
    }

    private static SubscriptionSchedulePhaseOptions BuildPhaseOptions(
        SubscriptionSchedulePhase sourcePhase,
        IReadOnlyList<OrganizationSubscriptionChange> changes,
        Plan source,
        Plan target,
        bool suppressDiscounts) =>
        new()
        {
            StartDate = sourcePhase.StartDate,
            EndDate = sourcePhase.EndDate,
            Items = ApplyChangesToPhaseItems(sourcePhase.Items, changes, source, target),
            Discounts = suppressDiscounts
                ? []
                : sourcePhase.Discounts?.Select(d =>
                    new SubscriptionSchedulePhaseDiscountOptions { Coupon = d.CouponId }).ToList(),
            Metadata = sourcePhase.Metadata,
            ProrationBehavior = sourcePhase.ProrationBehavior
        };

    private static List<SubscriptionSchedulePhaseItemOptions> ApplyChangesToPhaseItems(
        IList<SubscriptionSchedulePhaseItem> phaseItems,
        IReadOnlyList<OrganizationSubscriptionChange> changes,
        Plan sourcePlan,
        Plan targetPlan)
    {
        string Translate(string priceId) =>
            OrganizationPlanMigrationPriceMapper.MapOrPassThrough(priceId, sourcePlan, targetPlan);

        var items = phaseItems
            .Select(i => new SubscriptionSchedulePhaseItemOptions { Price = i.PriceId, Quantity = i.Quantity })
            .ToList();

        foreach (var change in changes)
        {
            change.Switch(
                addItem => items.Add(new SubscriptionSchedulePhaseItemOptions
                {
                    Price = Translate(addItem.PriceId),
                    Quantity = addItem.Quantity
                }),
                changeItemPrice =>
                {
                    var translatedCurrent = Translate(changeItemPrice.CurrentPriceId);
                    var translatedUpdated = Translate(changeItemPrice.UpdatedPriceId);
                    var existing = items.FirstOrDefault(i => i.Price == translatedCurrent);
                    if (existing != null)
                    {
                        existing.Price = translatedUpdated;
                        if (changeItemPrice.Quantity.HasValue)
                        {
                            existing.Quantity = changeItemPrice.Quantity.Value;
                        }
                    }
                },
                removeItem =>
                {
                    var translated = Translate(removeItem.PriceId);
                    items.RemoveAll(i => i.Price == translated);
                },
                updateItemQuantity =>
                {
                    var translated = Translate(updateItemQuantity.PriceId);
                    if (updateItemQuantity.Quantity == 0)
                    {
                        items.RemoveAll(i => i.Price == translated);
                    }
                    else
                    {
                        var existing = items.FirstOrDefault(i => i.Price == translated);
                        if (existing != null)
                        {
                            existing.Quantity = updateItemQuantity.Quantity;
                        }
                        else
                        {
                            items.Add(new SubscriptionSchedulePhaseItemOptions
                            {
                                Price = translated,
                                Quantity = updateItemQuantity.Quantity
                            });
                        }
                    }
                });
        }

        return items;
    }
}
