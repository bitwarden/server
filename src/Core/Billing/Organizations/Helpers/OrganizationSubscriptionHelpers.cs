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
    /// Loads the organization's Stripe subscription, optionally expanding the given paths.
    /// Returns null (and logs, tagged with <paramref name="caller"/>, at <paramref name="logLevel"/>)
    /// when Stripe reports the subscription is missing.
    /// </summary>
    /// <param name="logLevel">
    /// Defaults to <see cref="LogLevel.Error"/> for write-path callers, where a missing subscription
    /// means the operation cannot proceed. Read-path callers that run inline on a page load should
    /// pass <see cref="LogLevel.Warning"/> instead: a stale <c>GatewaySubscriptionId</c> is a data
    /// condition, not an operational failure, and should not page anyone on every page view.
    /// </param>
    public static async Task<Subscription?> TryGetSubscriptionAsync(
        IStripeAdapter stripeAdapter,
        ILogger logger,
        Organization organization,
        string caller,
        List<string>? expand = null,
        LogLevel logLevel = LogLevel.Error)
    {
        try
        {
            var options = expand is null ? null : new SubscriptionGetOptions { Expand = expand };
            return await stripeAdapter.GetSubscriptionAsync(organization.GatewaySubscriptionId, options);
        }
        catch (StripeException stripeException) when (stripeException.StripeError?.Code == ErrorCodes.ResourceMissing)
        {
            logger.Log(logLevel,
                "{Caller}: Subscription ({SubscriptionId}) for Organization ({OrganizationId}) was not found",
                caller, organization.GatewaySubscriptionId, organization.Id);
            return null;
        }
    }
}
