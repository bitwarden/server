using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Extensions;
using Bit.Core.Billing.Services;
using Bit.Core.Billing.Subscriptions.Models;
using Bit.Core.Services;
using Microsoft.Extensions.Logging;
using Stripe;
using static Bit.Core.Billing.Constants.StripeConstants;

namespace Bit.Core.Billing.Pricing;

public interface IPriceIncreaseScheduler
{
    /// <summary>
    /// Creates a two-phase subscription schedule that defers a price increase to the subscription's renewal date.
    /// Phase 1 echoes the current subscription state; Phase 2 applies the new price (and discount where applicable).
    /// Gated behind the <c>PM32645_DeferPriceMigrationToRenewal</c> feature flag. No-ops if the flag is off,
    /// an active schedule already exists, or the subscription does not match a known migration path.
    /// </summary>
    /// <param name="subscription">The Stripe subscription to schedule a price increase for.</param>
    /// <returns>True if a new schedule was created; false if skipped.</returns>
    Task<bool> Schedule(Subscription subscription);

    /// <summary>
    /// Releases any active subscription schedule for the given subscription, cancelling a pending
    /// deferred price increase. Use when the subscription operation makes the scheduled migration
    /// irrelevant (e.g., plan upgrade, sponsorship, cancellation). Gated behind the
    /// <c>PM32645_DeferPriceMigrationToRenewal</c> feature flag. Logs and re-throws on failure,
    /// requiring manual release via the Stripe Dashboard.
    /// </summary>
    /// <param name="customerId">The Stripe customer ID that owns the subscription.</param>
    /// <param name="subscriptionId">The Stripe subscription ID to release the schedule for.</param>
    Task Release(string customerId, string subscriptionId);

    /// <summary>
    /// Resolves the Phase 2 subscription schedule options for a price migration based on the subscription's
    /// current plan. Determines the appropriate target plan, pricing, and discount (if applicable) for
    /// supported migration paths (Premium and Families plans). Gated behind the
    /// <c>PM32645_DeferPriceMigrationToRenewal</c> feature flag.
    /// </summary>
    /// <param name="subscription">The Stripe subscription to resolve Phase 2 options for.</param>
    /// <returns>
    /// A <see cref="SubscriptionSchedulePhaseOptions"/> object containing the migration details if a supported
    /// migration path is found; otherwise, null if the feature flag is disabled, the subscription does not
    /// match a known migration path, or an error occurs during resolution.
    /// </returns>
    Task<SubscriptionSchedulePhaseOptions?> ResolvePhase2Async(Subscription subscription);
}

public class PriceIncreaseScheduler(
    IStripeAdapter stripeAdapter,
    IFeatureService featureService,
    IPricingClient pricingClient,
    ILogger<PriceIncreaseScheduler> logger) : IPriceIncreaseScheduler
{
    public async Task<bool> Schedule(Subscription subscription)
    {
        if (!featureService.IsEnabled(FeatureFlagKeys.PM32645_DeferPriceMigrationToRenewal))
        {
            return false;
        }

        var schedules = await stripeAdapter.ListSubscriptionSchedulesAsync(
            new SubscriptionScheduleListOptions { Customer = subscription.CustomerId });

        if (schedules.Data.Any(s => s.SubscriptionId == subscription.Id && s.Status == SubscriptionScheduleStatus.Active))
        {
            logger.LogInformation(
                "Active subscription schedule already exists for subscription ({SubscriptionId}), skipping schedule creation",
                subscription.Id);
            return false;
        }

        var phase2 = await ResolvePhase2Async(subscription);

        if (phase2 == null)
        {
            return false;
        }

        var schedule = await stripeAdapter.CreateSubscriptionScheduleAsync(
            new SubscriptionScheduleCreateOptions
            {
                FromSubscription = subscription.Id
            });

        try
        {
            var phase1 = schedule.Phases[0];

            await stripeAdapter.UpdateSubscriptionScheduleAsync(schedule.Id,
                new SubscriptionScheduleUpdateOptions
                {
                    EndBehavior = SubscriptionScheduleEndBehavior.Release,
                    Phases =
                    [
                        new SubscriptionSchedulePhaseOptions
                        {
                            StartDate = phase1.StartDate,
                            EndDate = phase1.EndDate,
                            Items = phase1.Items.Select(i => new SubscriptionSchedulePhaseItemOptions
                            {
                                Price = i.PriceId,
                                Quantity = i.Quantity
                            }).ToList(),
                            Discounts = phase1.Discounts?.Select(d => new SubscriptionSchedulePhaseDiscountOptions
                            {
                                Coupon = d.CouponId
                            }).ToList(),
                            ProrationBehavior = ProrationBehavior.None
                        },
                        phase2
                    ]
                });
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failed to update subscription schedule ({ScheduleId}) for subscription ({SubscriptionId}), attempting to release orphaned schedule",
                schedule.Id, subscription.Id);

            try
            {
                await stripeAdapter.ReleaseSubscriptionScheduleAsync(schedule.Id);
            }
            catch (Exception releaseEx)
            {
                logger.LogError(releaseEx,
                    "Failed to release orphaned subscription schedule ({ScheduleId}) for subscription ({SubscriptionId})",
                    schedule.Id, subscription.Id);
            }

            throw;
        }

        return true;
    }

    public async Task Release(string customerId, string subscriptionId)
    {
        if (!featureService.IsEnabled(FeatureFlagKeys.PM32645_DeferPriceMigrationToRenewal))
        {
            return;
        }

        try
        {
            var schedules = await stripeAdapter.ListSubscriptionSchedulesAsync(
                new SubscriptionScheduleListOptions { Customer = customerId });

            var activeSchedule = schedules.Data.FirstOrDefault(s =>
                s.Status == SubscriptionScheduleStatus.Active && s.SubscriptionId == subscriptionId);

            if (activeSchedule != null)
            {
                await stripeAdapter.ReleaseSubscriptionScheduleAsync(activeSchedule.Id);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failed to release subscription schedule for subscription {SubscriptionId}. Manual release required.",
                subscriptionId);
            throw;
        }
    }

    private async Task<SubscriptionSchedulePhaseOptions?> ResolvePhase2Async(Subscription subscription)
    {
        try
        {
            SubscriberId subscriberId = subscription;

            return await subscriberId.Match(
            _ => ResolvePhase2ForPremiumAsync(subscription),
            _ => ResolvePhase2ForFamiliesAsync(subscription),
            _ =>
            {
                logger.LogWarning(
                    "Provider subscriptions do not support price increase scheduling ({SubscriptionId})",
                    subscription.Id);
                return Task.FromResult<SubscriptionSchedulePhaseOptions?>(null);
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failed to resolve subscriber type for subscription ({SubscriptionId}), cannot determine price migration path",
                subscription.Id);
            return null;
        }
    }

    private async Task<SubscriptionSchedulePhaseOptions?> ResolvePhase2ForPremiumAsync(Subscription subscription)
    {
        var premiumPlans = await pricingClient.ListPremiumPlans();
        var oldPlan = premiumPlans.FirstOrDefault(p => !p.Available);
        var newPlan = premiumPlans.FirstOrDefault(p => p.Available);

        if (oldPlan == null || newPlan == null)
        {
            logger.LogError(
                "Could not resolve old and new premium plans for subscription ({SubscriptionId})",
                subscription.Id);
            return null;
        }

        if (subscription.Items.All(i => i.Price.Id != oldPlan.Seat.StripePriceId))
        {
            logger.LogWarning(
                "Subscription ({SubscriptionId}) does not have the old premium price, skipping schedule",
                subscription.Id);
            return null;
        }

        var items = new List<SubscriptionSchedulePhaseItemOptions>
        {
            new() { Price = newPlan.Seat.StripePriceId, Quantity = 1 }
        };

        var storageItem = subscription.Items.FirstOrDefault(i =>
            i.Price.Id == oldPlan.Storage.StripePriceId);

        if (storageItem is { Quantity: > 0 })
        {
            items.Add(new SubscriptionSchedulePhaseItemOptions
            {
                Price = newPlan.Storage.StripePriceId,
                Quantity = storageItem.Quantity
            });
        }

        return new SubscriptionSchedulePhaseOptions
        {
            StartDate = subscription.GetCurrentPeriodEnd(),
            Items = items,
            Discounts = [new() { Coupon = CouponIDs.Milestone2SubscriptionDiscount }],
            ProrationBehavior = ProrationBehavior.None
        };
    }

    private async Task<SubscriptionSchedulePhaseOptions?> ResolvePhase2ForFamiliesAsync(Subscription subscription)
    {
        var families2019 = await pricingClient.GetPlanOrThrow(PlanType.FamiliesAnnually2019);
        var families2025 = await pricingClient.GetPlanOrThrow(PlanType.FamiliesAnnually2025);
        var familiesTarget = await pricingClient.GetPlanOrThrow(PlanType.FamiliesAnnually);

        var oldPlan = subscription.Items.Any(i => i.Price.Id == families2019.PasswordManager.StripePlanId)
            ? families2019
            : subscription.Items.Any(i => i.Price.Id == families2025.PasswordManager.StripePlanId)
                ? families2025
                : null;

        if (oldPlan == null)
        {
            logger.LogWarning(
                "Could not determine families migration path for subscription ({SubscriptionId}), no matching plan found",
                subscription.Id);
            return null;
        }

        var items = new List<SubscriptionSchedulePhaseItemOptions>
        {
            new() { Price = familiesTarget.PasswordManager.StripePlanId, Quantity = 1 }
        };

        var storageItem = subscription.Items.FirstOrDefault(i =>
            i.Price.Id == oldPlan.PasswordManager.StripeStoragePlanId);

        if (storageItem is { Quantity: > 0 })
        {
            items.Add(new SubscriptionSchedulePhaseItemOptions
            {
                Price = familiesTarget.PasswordManager.StripeStoragePlanId,
                Quantity = storageItem.Quantity
            });
        }

        var discounts = oldPlan.Type == PlanType.FamiliesAnnually2019
            ? new List<SubscriptionSchedulePhaseDiscountOptions>
            {
                new() { Coupon = CouponIDs.Milestone3SubscriptionDiscount }
            }
            : null;

        return new SubscriptionSchedulePhaseOptions
        {
            StartDate = subscription.GetCurrentPeriodEnd(),
            Items = items,
            Discounts = discounts,
            ProrationBehavior = ProrationBehavior.None
        };
    }
}
