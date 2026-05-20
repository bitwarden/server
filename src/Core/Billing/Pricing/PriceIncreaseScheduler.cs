using Bit.Core.Billing.Enums;
using Bit.Core.Repositories;
using Bit.Core.Billing.Extensions;
using Bit.Core.Billing.Organizations.PlanMigration.Entities;
using Bit.Core.Billing.Organizations.PlanMigration.Repositories;
using Bit.Core.Billing.Organizations.PlanMigration.ValueObjects;
using Bit.Core.Billing.Services;
using Bit.Core.Billing.Subscriptions.Models;
using Bit.Core.Services;
using Microsoft.Extensions.Logging;
using Stripe;
using static Bit.Core.Billing.Constants.StripeConstants;
using Plan = Bit.Core.Models.StaticStore.Plan;

namespace Bit.Core.Billing.Pricing;

public interface IPriceIncreaseScheduler
{
    /// <summary>
    /// Creates a two-phase subscription schedule that defers a Premium/Families price increase
    /// to the subscription's renewal date. Phase 1 echoes the current subscription state;
    /// Phase 2 applies the new price (and discount where applicable). Gated behind the
    /// <c>PM32645_DeferPriceMigrationToRenewal</c> feature flag. No-ops if the flag is off,
    /// an active schedule already exists, or the subscription does not match a known personal
    /// migration path.
    /// </summary>
    /// <param name="subscription">The Stripe subscription to schedule a price increase for.</param>
    /// <returns>True if a new schedule was created; false if skipped.</returns>
    Task<bool> SchedulePersonalPriceIncrease(Subscription subscription);

    /// <summary>
    /// Creates a two-phase subscription schedule that defers a Teams/Enterprise 2020 -> current
    /// price increase to renewal. Phase 1 mirrors the current subscription; Phase 2 maps line
    /// items to the cohort's target plan and preserves existing discounts. Stamps
    /// <see cref="OrganizationPlanMigrationCohortAssignment.ScheduledDate"/> on success. Gated
    /// behind the <c>PM35215_BusinessPlanPriceMigration</c> feature flag.
    /// </summary>
    /// <remarks>
    /// Caller contract: <paramref name="subscription"/> must be loaded with <c>discounts</c>,
    /// <c>customer</c>, and <c>customer.discount</c> expanded, and <see cref="Subscription.Metadata"/>
    /// must contain an <c>organizationId</c> key (the scheduler throws if missing). The caller is
    /// responsible for confirming the organization belongs to <paramref name="cohort"/>.
    /// </remarks>
    /// <param name="subscription">The Stripe subscription, loaded with the expansions above.</param>
    /// <param name="cohort">The cohort the organization belongs to.</param>
    /// <returns>True if a new schedule was created; false if skipped.</returns>
    Task<bool> ScheduleBusinessPriceIncrease(Subscription subscription, OrganizationPlanMigrationCohort cohort);

    /// <summary>
    /// Creates a deferred price-increase schedule for the given subscription,
    /// dispatching to the correct path based on the subscription owner.
    /// </summary>
    /// <param name="subscription">The Stripe subscription to recover a schedule for.</param>
    /// <returns>True if a new schedule was created; false if skipped.</returns>
    Task<bool> ScheduleForSubscription(Subscription subscription);

    /// <summary>
    /// Releases any active subscription schedule for the given subscription, cancelling a pending
    /// deferred price increase. Use when the subscription operation makes the scheduled migration
    /// irrelevant (e.g., plan upgrade, sponsorship, cancellation). Runs when either
    /// <c>PM32645_DeferPriceMigrationToRenewal</c> or <c>PM35215_BusinessPlanPriceMigration</c> is
    /// enabled. Logs and re-throws on failure, requiring manual release via the Stripe Dashboard.
    /// </summary>
    /// <param name="customerId">The Stripe customer ID that owns the subscription.</param>
    /// <param name="subscriptionId">The Stripe subscription ID to release the schedule for.</param>
    Task Release(string customerId, string subscriptionId);
}

public class PriceIncreaseScheduler(
    IStripeAdapter stripeAdapter,
    IFeatureService featureService,
    IPricingClient pricingClient,
    IOrganizationRepository organizationRepository,
    IOrganizationPlanMigrationCohortAssignmentRepository assignmentRepository,
    IOrganizationPlanMigrationCohortRepository cohortRepository,
    ILogger<PriceIncreaseScheduler> logger) : IPriceIncreaseScheduler
{
    public async Task<bool> SchedulePersonalPriceIncrease(Subscription subscription)
    {
        if (!featureService.IsEnabled(FeatureFlagKeys.PM32645_DeferPriceMigrationToRenewal))
        {
            return false;
        }

        if (await ActiveScheduleExistsAsync(subscription))
        {
            return false;
        }

        var phase2 = await ResolvePersonalPhase2Async(subscription);
        if (phase2 == null)
        {
            return false;
        }

        await CreateAndConfigureScheduleAsync(subscription, phase2);
        return true;
    }

    public async Task<bool> ScheduleBusinessPriceIncrease(
        Subscription subscription,
        OrganizationPlanMigrationCohort cohort)
    {
        if (!featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration))
        {
            return false;
        }

        if (await ActiveScheduleExistsAsync(subscription))
        {
            return false;
        }

        Guid organizationId;
        try
        {
            SubscriberId subscriberId = subscription;
            var resolved = subscriberId.Match<Guid?>(
                _ =>
                {
                    logger.LogWarning(
                        "User subscriptions do not support business price increase scheduling ({SubscriptionId})",
                        subscription.Id);
                    return null;
                },
                orgId => orgId.Value,
                _ =>
                {
                    logger.LogWarning(
                        "Provider subscriptions do not support business price increase scheduling ({SubscriptionId})",
                        subscription.Id);
                    return null;
                });
            if (resolved is null)
            {
                return false;
            }
            organizationId = resolved.Value;
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failed to resolve subscriber type for subscription ({SubscriptionId}), cannot schedule business price increase",
                subscription.Id);
            return false;
        }

        var phase2 = await ResolvePhase2ForBusinessAsync(subscription, cohort);
        if (phase2 is null)
        {
            return false;
        }

        await CreateAndConfigureScheduleAsync(subscription, phase2);

        try
        {
            await stripeAdapter.UpdateSubscriptionAsync(subscription.Id,
                new SubscriptionUpdateOptions
                {
                    Metadata = new Dictionary<string, string>
                    {
                        [MetadataKeys.MigrationCohortId] = cohort.Id.ToString(),
                        [MetadataKeys.MigrationCohortName] = cohort.Name
                    }
                });
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failed to attach cohort metadata to subscription ({SubscriptionId}) for cohort ({CohortId}); migration is scheduled but Stripe dashboard attribution will be missing.",
                subscription.Id, cohort.Id);
        }

        var assignment = await assignmentRepository.GetByOrganizationIdAsync(organizationId);
        if (assignment is null)
        {
            // Schedule succeeded; assignment-row drift is left for reconciliation rather than failing the call.
            logger.LogError(
                "Created business price schedule for subscription ({SubscriptionId}) but no cohort assignment row found for cohort ({CohortId})",
                subscription.Id, cohort.Id);
            return true;
        }

        assignment.ScheduledDate = DateTime.UtcNow;
        assignment.RevisionDate = DateTime.UtcNow;
        await assignmentRepository.ReplaceAsync(assignment);

        logger.LogInformation(
            "Scheduled business price increase for subscription ({SubscriptionId}); assignment ({AssignmentId}) stamped with ScheduledDate",
            subscription.Id, assignment.Id);

        return true;
    }

    public async Task<bool> ScheduleForSubscription(Subscription subscription)
    {
        try
        {
            SubscriberId subscriberId = subscription;
            return await subscriberId.Match(
                _ => SchedulePersonalPriceIncrease(subscription),
                orgId => DispatchOrganizationScheduleAsync(subscription, orgId.Value),
                _ =>
                {
                    logger.LogWarning(
                        "Provider subscriptions do not support schedule recovery ({SubscriptionId})",
                        subscription.Id);
                    return Task.FromResult(false);
                });
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failed to resolve subscriber type for subscription ({SubscriptionId}), cannot recover schedule",
                subscription.Id);
            return false;
        }
    }

    public async Task Release(string customerId, string subscriptionId)
    {
        if (!featureService.IsEnabled(FeatureFlagKeys.PM32645_DeferPriceMigrationToRenewal) &&
            !featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration))
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

    private async Task<bool> ActiveScheduleExistsAsync(Subscription subscription)
    {
        var schedules = await stripeAdapter.ListSubscriptionSchedulesAsync(
            new SubscriptionScheduleListOptions { Customer = subscription.CustomerId });

        var exists = schedules.Data.Any(s =>
            s.SubscriptionId == subscription.Id && s.Status == SubscriptionScheduleStatus.Active);

        if (exists)
        {
            logger.LogInformation(
                "Active subscription schedule already exists for subscription ({SubscriptionId}), skipping schedule creation",
                subscription.Id);
        }

        return exists;
    }

    private async Task<SubscriptionSchedule> CreateAndConfigureScheduleAsync(
        Subscription subscription,
        SubscriptionSchedulePhaseOptions phase2)
    {
        var schedule = await stripeAdapter.CreateSubscriptionScheduleAsync(
            new SubscriptionScheduleCreateOptions { FromSubscription = subscription.Id });

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
                            Items = [.. phase1.Items.Select(i => new SubscriptionSchedulePhaseItemOptions
                            {
                                Price = i.PriceId,
                                Quantity = i.Quantity
                            })],
                            Discounts = phase1.Discounts is null ? null :
                            [
                                .. phase1.Discounts.Select(d => new SubscriptionSchedulePhaseDiscountOptions
                                {
                                    Coupon = d.CouponId
                                })
                            ],
                            ProrationBehavior = ProrationBehavior.None
                        },
                        phase2
                    ]
                });

            return schedule;
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
    }

    private async Task<SubscriptionSchedulePhaseOptions?> ResolvePersonalPhase2Async(Subscription subscription)
    {
        if (!featureService.IsEnabled(FeatureFlagKeys.PM32645_DeferPriceMigrationToRenewal))
        {
            return null;
        }

        // Stripe.NET deserializes an unexpanded "discounts" array as a list of null entries;
        // proceeding would silently drop pre-existing discounts from Phase 2.
        if (subscription.Discounts is { Count: > 0 } && subscription.Discounts.Any(d => d == null))
        {
            logger.LogError(
                "Subscription ({SubscriptionId}) was loaded without expanding 'discounts'; " +
                "{Count} pre-existing discount(s) would be silently dropped from Phase 2. " +
                "Caller must include \"discounts\" in the Stripe Expand list.",
                subscription.Id, subscription.DiscountIds?.Count ?? 0);
            return null;
        }

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
                "Failed to resolve Phase 2 options for subscription ({SubscriptionId}), cannot determine price migration path",
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

        var startDate = subscription.GetCurrentPeriodEnd();
        if (startDate == null)
        {
            logger.LogError(
                "Could not determine current period end for subscription ({SubscriptionId}), skipping schedule creation",
                subscription.Id);
            return null;
        }

        List<SubscriptionSchedulePhaseDiscountOptions> discounts = [..
            subscription.Discounts?.Select(d => new SubscriptionSchedulePhaseDiscountOptions { Coupon = d.Coupon.Id }) ?? []];

        discounts.Add(new SubscriptionSchedulePhaseDiscountOptions
        {
            Coupon = CouponIDs.Milestone2SubscriptionDiscount
        });

        return new SubscriptionSchedulePhaseOptions
        {
            StartDate = startDate,
            EndDate = startDate.Value.AddYears(1),
            Items = items,
            Discounts = discounts,
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

        List<SubscriptionSchedulePhaseDiscountOptions> discounts = [..
            subscription.Discounts?.Select(d => new SubscriptionSchedulePhaseDiscountOptions { Coupon = d.Coupon.Id }) ?? []];

        if (oldPlan.Type == PlanType.FamiliesAnnually2019)
        {
            discounts.Add(new SubscriptionSchedulePhaseDiscountOptions
            {
                Coupon = CouponIDs.Milestone3SubscriptionDiscount
            });
        }

        var startDate = subscription.GetCurrentPeriodEnd();
        if (startDate == null)
        {
            logger.LogError(
                "Could not determine current period end for subscription ({SubscriptionId}), skipping schedule creation",
                subscription.Id);
            return null;
        }

        return new SubscriptionSchedulePhaseOptions
        {
            StartDate = startDate,
            EndDate = startDate.Value.AddYears(1),
            Items = items,
            Discounts = discounts.Count > 0 ? discounts : null,
            ProrationBehavior = ProrationBehavior.None
        };
    }

    private async Task<SubscriptionSchedulePhaseOptions?> ResolvePhase2ForBusinessAsync(
        Subscription subscription,
        OrganizationPlanMigrationCohort cohort)
    {
        // Stripe.NET deserializes an unexpanded "discounts" array as a list of null entries;
        // proceeding would silently drop pre-existing discounts from Phase 2.
        if (subscription.Discounts is { Count: > 0 } && subscription.Discounts.Any(d => d == null))
        {
            logger.LogError(
                "Subscription ({SubscriptionId}) was loaded without expanding 'discounts'; " +
                "{Count} pre-existing discount(s) would be silently dropped from Phase 2. " +
                "Caller must include \"discounts\" in the Stripe Expand list.",
                subscription.Id, subscription.DiscountIds?.Count ?? 0);
            return null;
        }

        if (cohort.MigrationPathId is null)
        {
            // Churn-only cohort — no migration to schedule.
            return null;
        }

        var migrationPath = MigrationPaths.FromId(cohort.MigrationPathId.Value);
        if (migrationPath is null)
        {
            logger.LogError(
                "Unknown MigrationPathId ({MigrationPathId}) on cohort ({CohortId}); cannot schedule business price increase for subscription ({SubscriptionId})",
                cohort.MigrationPathId, cohort.Id, subscription.Id);
            return null;
        }

        var sourcePlan = await pricingClient.GetPlanOrThrow(migrationPath.FromPlan);
        var targetPlan = await pricingClient.GetPlanOrThrow(migrationPath.ToPlan);

        var items = new List<SubscriptionSchedulePhaseItemOptions>();
        foreach (var item in subscription.Items.Data)
        {
            var targetPriceId = MapToTargetPriceId(item.Price.Id, sourcePlan, targetPlan);
            if (targetPriceId is null)
            {
                logger.LogWarning(
                    "Subscription ({SubscriptionId}) line item price ({PriceId}) has no mapping in migration path {PathName}; skipping schedule",
                    subscription.Id, item.Price.Id, migrationPath.Name);
                return null;
            }
            items.Add(new SubscriptionSchedulePhaseItemOptions
            {
                Price = targetPriceId,
                Quantity = item.Quantity
            });
        }

        var discounts = new List<SubscriptionSchedulePhaseDiscountOptions>();

        if (subscription.Customer?.Discount?.Coupon?.Id is { Length: > 0 } customerCouponId)
        {
            discounts.Add(new SubscriptionSchedulePhaseDiscountOptions { Coupon = customerCouponId });
        }

        if (subscription.Discounts is not null)
        {
            discounts.AddRange(subscription.Discounts.Select(d =>
                new SubscriptionSchedulePhaseDiscountOptions { Coupon = d.Coupon.Id }));
        }

        if (!string.IsNullOrEmpty(cohort.ProactiveDiscountCouponCode))
        {
            discounts.Add(new SubscriptionSchedulePhaseDiscountOptions
            {
                Coupon = cohort.ProactiveDiscountCouponCode
            });
        }

        if (subscription.GetCurrentPeriod() is not { Start: { } currentStart, End: { } currentEnd })
        {
            logger.LogError(
                "Could not determine current period for subscription ({SubscriptionId}); skipping business schedule creation",
                subscription.Id);
            return null;
        }

        var periodLength = currentEnd - currentStart;

        return new SubscriptionSchedulePhaseOptions
        {
            StartDate = currentEnd,
            EndDate = currentEnd + periodLength,
            Items = items,
            Discounts = discounts.Count > 0 ? discounts : null,
            ProrationBehavior = ProrationBehavior.None
        };
    }

    private static string? MapToTargetPriceId(string sourcePriceId, Plan source, Plan target) => sourcePriceId switch
    {
        _ when sourcePriceId == source.PasswordManager.StripeSeatPlanId => target.PasswordManager.StripeSeatPlanId,
        _ when sourcePriceId == source.PasswordManager.StripeStoragePlanId => target.PasswordManager.StripeStoragePlanId,
        _ when sourcePriceId == source.SecretsManager?.StripeSeatPlanId => target.SecretsManager?.StripeSeatPlanId,
        _ when sourcePriceId == source.SecretsManager?.StripeServiceAccountPlanId => target.SecretsManager?.StripeServiceAccountPlanId,
        _ => null
    };

    /// <summary>
    /// Dispatches a price increase schedule for a subscription to an organization.
    /// </summary>
    /// <param name="subscription"> The subscription to schedule a price increase for. </param>
    /// <param name="organizationId"> The ID of the organization associated with the subscription. </param>
    /// <returns> True if the schedule was dispatched successfully, false otherwise. </returns>
    private async Task<bool> DispatchOrganizationScheduleAsync(Subscription subscription, Guid organizationId)
    {
        var organization = await organizationRepository.GetByIdAsync(organizationId);
        if (organization is null)
        {
            logger.LogError(
                "Organization ({OrganizationId}) not found; cannot recover schedule for subscription ({SubscriptionId})",
                organizationId, subscription.Id);
            return false;
        }

        if (!IsTrackABusinessPlanType(organization.PlanType))
        {
            return await SchedulePersonalPriceIncrease(subscription);
        }

        var assignment = await assignmentRepository.GetByOrganizationIdAsync(organizationId);
        if (assignment is null)
        {
            return false;
        }

        var cohort = await cohortRepository.GetByIdAsync(assignment.CohortId);
        if (cohort is null || !cohort.IsActive)
        {
            return false;
        }

        if (cohort.MigrationPathId is null)
        {
            // Churn-only cohort — no migration to schedule.
            return false;
        }

        var migrationPath = MigrationPaths.FromId(cohort.MigrationPathId.Value);
        if (migrationPath is null)
        {
            logger.LogError(
                "Unknown MigrationPathId ({MigrationPathId}) on cohort ({CohortId}); skipping schedule recovery for subscription ({SubscriptionId})",
                cohort.MigrationPathId, cohort.Id, subscription.Id);
            return false;
        }

        if (organization.PlanType != migrationPath.FromPlan)
        {
            logger.LogWarning(
                "Skipping schedule recovery for Organization ({OrganizationId}); PlanType {ActualPlan} does not match cohort {CohortName} source {ExpectedPlan}",
                organizationId, organization.PlanType, cohort.Name, migrationPath.FromPlan);
            return false;
        }

        return await ScheduleBusinessPriceIncrease(subscription, cohort);
    }

    /// <summary>
    /// Returns true if the plan type is a Track A business plan type.
    /// </summary>
    /// <param name="planType">The plan type to check.</param>
    /// <returns>True if the plan type is a Track A business plan type, otherwise false.</returns>
    /// <remarks> This method should be expanded to include other track business plan types as needed.</remarks>
    private static bool IsTrackABusinessPlanType(PlanType planType) => planType is
        PlanType.TeamsMonthly2020 or
        PlanType.TeamsAnnually2020 or
        PlanType.EnterpriseMonthly2020 or
        PlanType.EnterpriseAnnually2020;

}
