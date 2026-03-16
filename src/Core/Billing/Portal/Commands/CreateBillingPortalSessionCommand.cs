using Bit.Core.Billing.Commands;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Services;
using Bit.Core.Entities;
using Microsoft.Extensions.Logging;
using Stripe;
using Stripe.BillingPortal;

namespace Bit.Core.Billing.Portal.Commands;

using static StripeConstants;

public interface ICreateBillingPortalSessionCommand
{
    Task<BillingCommandResult<string>> Run(User user, string returnUrl);
}

public class CreateBillingPortalSessionCommand(
    ILogger<CreateBillingPortalSessionCommand> logger,
    IStripeAdapter stripeAdapter)
    : BaseBillingCommand<CreateBillingPortalSessionCommand>(logger), ICreateBillingPortalSessionCommand
{
    private readonly ILogger<CreateBillingPortalSessionCommand> _logger = logger;

    protected override Conflict DefaultConflict =>
        new("Unable to create billing portal session. Please contact support for assistance.");

    public Task<BillingCommandResult<string>> Run(User user, string returnUrl) =>
        HandleAsync<string>(async () =>
        {
            if (string.IsNullOrEmpty(user.GatewayCustomerId))
            {
                _logger.LogWarning("{Command}: User ({UserId}) does not have a Stripe customer ID",
                    CommandName, user.Id);
                return new BadRequest("User does not have a Stripe customer ID.");
            }

            if (string.IsNullOrEmpty(user.GatewaySubscriptionId))
            {
                _logger.LogWarning("{Command}: User ({UserId}) does not have a subscription",
                    CommandName, user.Id);
                return new BadRequest("User does not have a Premium subscription.");
            }

            // Fetch the subscription to validate its status
            Subscription subscription;
            try
            {
                subscription = await stripeAdapter.GetSubscriptionAsync(user.GatewaySubscriptionId);
            }
            catch (StripeException stripeException)
            {
                _logger.LogError(stripeException,
                    "{Command}: Failed to fetch subscription ({SubscriptionId}) for user ({UserId})",
                    CommandName, user.GatewaySubscriptionId, user.Id);
                return new BadRequest("Unable to verify subscription status.");
            }

            if (subscription == null)
            {
                _logger.LogWarning("{Command}: Subscription ({SubscriptionId}) for user ({UserId}) was not found",
                    CommandName, user.GatewaySubscriptionId, user.Id);
                return new BadRequest("User subscription not found.");
            }

            // Only allow portal access for active or past_due subscriptions
            if (subscription.Status != SubscriptionStatus.Active && subscription.Status != SubscriptionStatus.PastDue)
            {
                _logger.LogWarning(
                    "{Command}: User ({UserId}) subscription ({SubscriptionId}) has status '{Status}' which is not eligible for portal access",
                    CommandName, user.Id, user.GatewaySubscriptionId, subscription.Status);
                return new BadRequest("Your subscription cannot be managed in its current status.");
            }

            var options = new SessionCreateOptions
            {
                Customer = user.GatewayCustomerId,
                ReturnUrl = returnUrl
            };

            var session = await stripeAdapter.CreateBillingPortalSessionAsync(options);

            if (session?.Url == null)
            {
                return DefaultConflict;
            }

            _logger.LogInformation("{Command}: Successfully created billing portal session for user ({UserId})",
                CommandName, user.Id);

            return session.Url;
        });
}
