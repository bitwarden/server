using Bit.Core.Billing.Models;
using Bit.Core.Services;
using Microsoft.Extensions.Logging;
using Stripe;

using static Bit.Core.Billing.Utilities;

namespace Bit.Core.Billing.Commands.Implementations;

public class CancelSubscriptionCommand(
    ILogger<CancelSubscriptionCommand> logger,
    IStripeAdapter stripeAdapter)
    : ICancelSubscriptionCommand
{
    private static readonly List<string> _validReasons =
    [
        "customer_service",
        "low_quality",
        "missing_features",
        "other",
        "switched_service",
        "too_complex",
        "too_expensive",
        "unused"
    ];

    public async Task CancelSubscription(
        Subscription subscription,
        OffboardingSurveyResponse offboardingSurveyResponse,
        bool cancelImmediately)
    {
        if (IsInactive(subscription))
        {
            logger.LogWarning("Cannot cancel subscription ({ID}) that's already inactive.", subscription.Id);
            throw ContactSupport();
        }

        var metadata = new Dictionary<string, string>
        {
            { "cancellingUserId", offboardingSurveyResponse.UserId.ToString() }
        };

        if (cancelImmediately)
        {
            if (BelongsToOrganization(subscription))
            {
                await stripeAdapter.SubscriptionUpdateAsync(subscription.Id, new SubscriptionUpdateOptions
                {
                    Metadata = metadata
                });
            }

            await CancelSubscriptionImmediatelyAsync(subscription.Id, offboardingSurveyResponse);
        }
        else
        {
            await CancelSubscriptionAtEndOfPeriodAsync(subscription.Id, offboardingSurveyResponse, metadata);
        }
    }

    private static bool BelongsToOrganization(IHasMetadata subscription)
        => subscription.Metadata != null && subscription.Metadata.ContainsKey("organizationId");

    private async Task CancelSubscriptionImmediatelyAsync(
        string subscriptionId,
        OffboardingSurveyResponse offboardingSurveyResponse)
    {
        var options = new SubscriptionCancelOptions
        {
            CancellationDetails = new SubscriptionCancellationDetailsOptions
            {
                Comment = offboardingSurveyResponse.Feedback
            }
        };

        if (IsValidCancellationReason(offboardingSurveyResponse.Reason))
        {
            options.CancellationDetails.Feedback = offboardingSurveyResponse.Reason;
        }

        await stripeAdapter.SubscriptionCancelAsync(subscriptionId, options);
    }

    private static bool IsInactive(Subscription subscription) =>
        subscription.CanceledAt.HasValue ||
        subscription.Status == "canceled" ||
        subscription.Status == "unpaid" ||
        subscription.Status == "incomplete_expired";

    private static bool IsValidCancellationReason(string reason) => _validReasons.Contains(reason);

    private async Task CancelSubscriptionAtEndOfPeriodAsync(
        string subscriptionId,
        OffboardingSurveyResponse offboardingSurveyResponse,
        Dictionary<string, string> metadata = null)
    {
        var options = new SubscriptionUpdateOptions
        {
            CancelAtPeriodEnd = true,
            CancellationDetails = new SubscriptionCancellationDetailsOptions
            {
                Comment = offboardingSurveyResponse.Feedback
            }
        };

        if (IsValidCancellationReason(offboardingSurveyResponse.Reason))
        {
            options.CancellationDetails.Feedback = offboardingSurveyResponse.Reason;
        }

        if (metadata != null)
        {
            options.Metadata = metadata;
        }

        await stripeAdapter.SubscriptionUpdateAsync(subscriptionId, options);
    }
}
