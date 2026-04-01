using Bit.Core.Billing.Commands;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Services;
using Bit.Core.Entities;
using Bit.Core.Services;
using Microsoft.Extensions.Logging;
using OneOf.Types;
using Stripe;

namespace Bit.Core.Billing.Subscriptions.Commands;

using static StripeConstants;

public interface IReinstateSubscriptionCommand
{
    Task<BillingCommandResult<None>> Run(ISubscriber subscriber);
}

public class ReinstateSubscriptionCommand(
    ILogger<ReinstateSubscriptionCommand> logger,
    IStripeAdapter stripeAdapter,
    IFeatureService featureService,
    IPriceIncreaseScheduler priceIncreaseScheduler) : BaseBillingCommand<ReinstateSubscriptionCommand>(logger), IReinstateSubscriptionCommand
{
    private readonly ILogger<ReinstateSubscriptionCommand> _logger = logger;
    protected override Conflict DefaultConflict => new("We had a problem reinstating your subscription. Please contact support for assistance.");

    public Task<BillingCommandResult<None>> Run(ISubscriber subscriber) => HandleAsync<None>(async () =>
    {
        var subscription = await stripeAdapter.GetSubscriptionAsync(subscriber.GatewaySubscriptionId);

        if (subscription is not
            {
                Status: SubscriptionStatus.Trialing or SubscriptionStatus.Active,
                CancelAt: not null
            })
        {
            return new BadRequest("Subscription is not pending cancellation.");
        }

        if (featureService.IsEnabled(FeatureFlagKeys.PM32645_DeferPriceMigrationToRenewal))
        {
            var activeSchedule = await GetActiveScheduleAsync(subscription);

            // if there is an active schedule, we need to update it to include Phase 2 because it was removed during cancellation
            if (activeSchedule is { Phases.Count: > 0 })
            {
                if (activeSchedule.Phases.Count > 1)
                {
                    _logger.LogWarning(
                        "{Command}: Subscription schedule ({ScheduleId}) has {PhaseCount} phases (expected 1 after cancellation), updating to add Phase 2",
                        CommandName, activeSchedule.Id, activeSchedule.Phases.Count);
                }

                _logger.LogInformation(
                    "{Command}: Active subscription schedule ({ScheduleId}) found for subscription ({SubscriptionId}), updating schedule phases",
                    CommandName, activeSchedule.Id, subscription.Id);

                var phase2 = await priceIncreaseScheduler.ResolvePhase2Async(subscription);
                if (phase2 == null)
                {
                    _logger.LogError("Failed to resolve Phase 2 for Subscription {SubscriptionId}", subscription.Id);
                    return DefaultConflict;
                }
                var phase1 = activeSchedule.Phases[0];

                await stripeAdapter.UpdateSubscriptionScheduleAsync(activeSchedule.Id,
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
                return new None();
            }
        }

        // The default behavior for non-price-migration subscriptions or subscriptions without
        // active schedules is to simply not cancel at the end of the period.
        await stripeAdapter.UpdateSubscriptionAsync(subscription.Id, new SubscriptionUpdateOptions
        {
            CancelAtPeriodEnd = false
        });

        return new None();
    });

    private async Task<SubscriptionSchedule?> GetActiveScheduleAsync(Subscription subscription)
    {
        var schedules = await stripeAdapter.ListSubscriptionSchedulesAsync(
            new SubscriptionScheduleListOptions { Customer = subscription.CustomerId });

        return schedules.Data.FirstOrDefault(s =>
            s.SubscriptionId == subscription.Id &&
            s.Status == SubscriptionScheduleStatus.Active);
    }
}
