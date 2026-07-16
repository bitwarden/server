using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Services;
using Microsoft.Extensions.Logging;
using Stripe;

namespace Bit.Core.Billing.Organizations.AnnualUpgradeOffer;

using static StripeConstants;

public static class AnnualUpgradeOfferSubscriptionHelpers
{
    /// <summary>
    /// Loads the organization's Stripe subscription, optionally expanding the given paths.
    /// Returns null (and logs an error tagged with <paramref name="caller"/>) when Stripe
    /// reports the subscription is missing.
    /// </summary>
    public static async Task<Subscription?> TryGetSubscriptionAsync(
        IStripeAdapter stripeAdapter,
        ILogger logger,
        Organization organization,
        string caller,
        List<string>? expand = null)
    {
        try
        {
            var options = expand is null ? null : new SubscriptionGetOptions { Expand = expand };
            return await stripeAdapter.GetSubscriptionAsync(organization.GatewaySubscriptionId, options);
        }
        catch (StripeException stripeException) when (stripeException.StripeError?.Code == ErrorCodes.ResourceMissing)
        {
            logger.LogError(
                "{Caller}: Subscription ({SubscriptionId}) for Organization ({OrganizationId}) was not found",
                caller, organization.GatewaySubscriptionId, organization.Id);
            return null;
        }
    }
}
