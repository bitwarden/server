using Bit.Core.Billing.Commands;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Services;
using Bit.Core.Entities;
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
    IStripeAdapter stripeAdapter) : BaseBillingCommand<ReinstateSubscriptionCommand>(logger), IReinstateSubscriptionCommand
{
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

        await stripeAdapter.UpdateSubscriptionAsync(subscription.Id, new SubscriptionUpdateOptions
        {
            CancelAtPeriodEnd = false
        });

        return new None();
    });
}
