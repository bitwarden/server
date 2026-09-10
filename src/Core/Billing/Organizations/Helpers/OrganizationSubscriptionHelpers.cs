using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Services;
using Microsoft.Extensions.Logging;
using Stripe;

namespace Bit.Core.Billing.Organizations.Helpers;

using static StripeConstants;

public static class OrganizationSubscriptionHelpers
{
    /// <summary>
    /// Loads the organization's Stripe subscription, optionally expanding the given paths. Returns
    /// null and logs a Warning, tagged with the calling class, when Stripe reports the subscription
    /// is missing.
    /// </summary>
    public static async Task<Subscription?> TryGetSubscriptionAsync<T>(
        IStripeAdapter stripeAdapter,
        ILogger<T> logger,
        Organization organization,
        List<string>? expand = null)
    {
        try
        {
            var options = expand is null ? null : new SubscriptionGetOptions { Expand = expand };
            return await stripeAdapter.GetSubscriptionAsync(organization.GatewaySubscriptionId, options);
        }
        catch (StripeException stripeException) when (stripeException.StripeError?.Code == ErrorCodes.ResourceMissing)
        {
            logger.LogWarning(
                "{Caller}: Subscription ({SubscriptionId}) for Organization ({OrganizationId}) was not found",
                typeof(T).Name, organization.GatewaySubscriptionId, organization.Id);
            return null;
        }
    }
}
