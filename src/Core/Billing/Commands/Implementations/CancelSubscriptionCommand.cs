using Bit.Core.Billing.Models;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Services;
using Microsoft.Extensions.Logging;
using Stripe;

namespace Bit.Core.Billing.Commands.Implementations;

public class CancelSubscriptionCommand : ICancelSubscriptionCommand
{
    private readonly ILogger<CancelSubscriptionCommand> _logger;
    private readonly IStripeAdapter _stripeAdapter;

    private static readonly List<string> _validReasons = new()
    {
        "customer_service",
        "low_quality",
        "missing_features",
        "other",
        "switched_service",
        "too_complex",
        "too_expensive",
        "unused"
    };

    public CancelSubscriptionCommand(
        ILogger<CancelSubscriptionCommand> logger,
        IStripeAdapter stripeAdapter)
    {
        _logger = logger;
        _stripeAdapter = stripeAdapter;
    }

    public async Task CancelSubscription(
        ISubscriber subscriber,
        OffboardingSurveyResponse offboardingSurveyResponse)
    {
        var subscription = await GetSubscriptionAsync(subscriber);

        if (IsInactive(subscription))
        {
            _logger.LogWarning("Cannot cancel subscription ({ID}) that's already inactive.", subscription.Id);
            return;
        }

        var metadata = new Dictionary<string, string>
        {
            { "cancellingUserId", offboardingSurveyResponse.UserId.ToString() }
        };

        if (subscriber.IsExpired())
        {
            if (subscriber.IsOrganization())
            {
                await _stripeAdapter.SubscriptionUpdateAsync(subscription.Id, new SubscriptionUpdateOptions
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

        await _stripeAdapter.SubscriptionCancelAsync(subscriptionId, options);
    }

    private static GatewayException ContactSupport() => new("Something went wrong when trying to cancel your subscription. Please contact support.");

    private async Task<Subscription> GetSubscriptionAsync(ISubscriber subscriber)
    {
        ArgumentNullException.ThrowIfNull(subscriber);

        if (string.IsNullOrEmpty(subscriber.GatewaySubscriptionId))
        {
            _logger.LogError("Cannot cancel subscription for subscriber ({ID}) with no GatewaySubscriptionId.", subscriber.Id);

            throw ContactSupport();
        }

        var subscription = await _stripeAdapter.SubscriptionGetAsync(subscriber.GatewaySubscriptionId);

        if (subscription != null)
        {
            return subscription;
        }

        _logger.LogError("Could not find Stripe subscription ({ID}) to cancel.", subscriber.GatewaySubscriptionId);

        throw ContactSupport();
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

        await _stripeAdapter.SubscriptionUpdateAsync(subscriptionId, options);
    }
}
