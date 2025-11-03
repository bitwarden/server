using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Commands;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Extensions;
using Bit.Core.Billing.Services;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Core.Services;
using OneOf.Types;
using Stripe;

namespace Bit.Core.Billing.Subscriptions.Commands;

using static StripeConstants;

public interface IRestartSubscriptionCommand
{
    Task<BillingCommandResult<None>> Run(
        ISubscriber subscriber);
}

public class RestartSubscriptionCommand(
    IOrganizationRepository organizationRepository,
    IProviderRepository providerRepository,
    IStripeAdapter stripeAdapter,
    ISubscriberService subscriberService,
    IUserRepository userRepository) : IRestartSubscriptionCommand
{
    public async Task<BillingCommandResult<None>> Run(
        ISubscriber subscriber)
    {
        var existingSubscription = await subscriberService.GetSubscription(subscriber);

        if (existingSubscription is not { Status: SubscriptionStatus.Canceled })
        {
            return new BadRequest("Cannot restart a subscription that is not canceled.");
        }

        var options = new SubscriptionCreateOptions
        {
            AutomaticTax = new SubscriptionAutomaticTaxOptions { Enabled = true },
            CollectionMethod = CollectionMethod.ChargeAutomatically,
            Customer = existingSubscription.CustomerId,
            Items = existingSubscription.Items.Select(subscriptionItem => new SubscriptionItemOptions
            {
                Price = subscriptionItem.Price.Id,
                Quantity = subscriptionItem.Quantity
            }).ToList(),
            Metadata = existingSubscription.Metadata,
            OffSession = true,
            TrialPeriodDays = 0
        };

        var subscription = await stripeAdapter.SubscriptionCreateAsync(options);
        await EnableAsync(subscriber, subscription);
        return new None();
    }

    private async Task EnableAsync(ISubscriber subscriber, Subscription subscription)
    {
        switch (subscriber)
        {
            case Organization organization:
                {
                    organization.GatewaySubscriptionId = subscription.Id;
                    organization.Enabled = true;
                    organization.ExpirationDate = subscription.GetCurrentPeriodEnd();
                    organization.RevisionDate = DateTime.UtcNow;
                    await organizationRepository.ReplaceAsync(organization);
                    break;
                }
            case Provider provider:
                {
                    provider.GatewaySubscriptionId = subscription.Id;
                    provider.Enabled = true;
                    provider.RevisionDate = DateTime.UtcNow;
                    await providerRepository.ReplaceAsync(provider);
                    break;
                }
            case User user:
                {
                    user.GatewaySubscriptionId = subscription.Id;
                    user.Premium = true;
                    user.PremiumExpirationDate = subscription.GetCurrentPeriodEnd();
                    user.RevisionDate = DateTime.UtcNow;
                    await userRepository.ReplaceAsync(user);
                    break;
                }
        }
    }
}
