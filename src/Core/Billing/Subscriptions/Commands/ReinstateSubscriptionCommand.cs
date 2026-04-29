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
            if (subscription.Metadata?.ContainsKey(MetadataKeys.CancelledDuringDeferredPriceIncrease) == true)
            {
                _logger.LogInformation(
                    "{Command}: Subscription ({SubscriptionId}) has pending price increase, clearing flag and recreating schedule",
                    CommandName, subscription.Id);

                // Clear pending cancellation and flag BEFORE attaching a schedule.
                // Stripe discourages direct subscription updates once a schedule is attached as it can create inconsistencies in phases.
                await stripeAdapter.UpdateSubscriptionAsync(subscription.Id, new SubscriptionUpdateOptions
                {
                    CancelAtPeriodEnd = false,
                    Metadata = new Dictionary<string, string>
                    {
                        [MetadataKeys.CancelledDuringDeferredPriceIncrease] = ""
                    }
                });

                await priceIncreaseScheduler.Schedule(subscription);

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
}
