using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Extensions;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Services;
using Bit.Core.Billing.Subscriptions.Models;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Invoicing.InvoicePreviews.Models;
using Microsoft.Extensions.Logging;
using Stripe;

namespace Bit.Invoicing.InvoicePreviews.Queries;

using static StripeConstants;

public interface IGetSubscriptionPreviewQuery
{
    /// <summary>
    /// Builds the <see cref="SubscriptionPreview"/> for a subscriber's upcoming renewal: the invoice
    /// preview the customer will actually be billed against, wrapped in the subscription-level envelope
    /// (status, storage, cancellation, and suspension details).
    /// </summary>
    /// <returns>The preview, or null when the subscriber has no Stripe subscription to read.</returns>
    Task<SubscriptionPreview?> Run(ISubscriber subscriber);
}

public class GetSubscriptionPreviewQuery(
    ILogger<GetSubscriptionPreviewQuery> logger,
    IPricingClient pricingClient,
    IStripeAdapter stripeAdapter,
    IInvoicePreviewService invoicePreviewService) : IGetSubscriptionPreviewQuery
{
    public async Task<SubscriptionPreview?> Run(ISubscriber subscriber)
    {
        if (string.IsNullOrEmpty(subscriber.GatewaySubscriptionId))
        {
            return null;
        }

        var subscription = await FetchSubscriptionAsync(subscriber);
        if (subscription == null)
        {
            return null;
        }

        var (planTier, cadence) = await ResolveTierAndCadenceAsync(subscriber);
        var invoicePreview = await GetInvoicePreviewAsync(subscription, planTier, cadence);

        // Storage has an implicit conversion from Organization/User that yields null when there's no max storage.
        Storage? storage = subscriber switch
        {
            Organization organization => organization,
            User user => user,
            _ => null
        };

        var preview = new SubscriptionPreview
        {
            Status = subscription.Status,
            InvoicePreview = invoicePreview,
            Storage = storage
        };

        switch (subscription.Status)
        {
            case SubscriptionStatus.Incomplete:
            case SubscriptionStatus.IncompleteExpired:
                return preview with { Suspension = subscription.Created.AddHours(23), GracePeriod = 1 };

            case SubscriptionStatus.Trialing:
            case SubscriptionStatus.Active:
                return preview with
                {
                    InvoicePreview = invoicePreview with { NextPaymentAttempt = subscription.GetCurrentPeriodEnd() },
                    CancelAt = subscription.CancelAt
                };

            case SubscriptionStatus.PastDue:
            case SubscriptionStatus.Unpaid:
                var suspension = await Utilities.GetSubscriptionSuspensionAsync(stripeAdapter, subscription);
                return suspension == null
                    ? preview
                    : preview with { Suspension = suspension.SuspensionDate, GracePeriod = suspension.GracePeriod };

            case SubscriptionStatus.Canceled:
                return preview with { Canceled = subscription.CanceledAt };

            default:
                logger.LogError("Subscription ({SubscriptionId}) has an unmanaged status ({Status})",
                    subscription.Id, subscription.Status);
                throw new ConflictException("Subscription is in an invalid state. Please contact support for assistance.");
        }
    }

    private async Task<InvoicePreview> GetInvoicePreviewAsync(
        Subscription subscription, PlanTierType planTier, PlanCadenceType cadence)
    {
        try
        {
            var options = new InvoiceCreatePreviewOptions { Subscription = subscription.Id };
            return await invoicePreviewService.GetInvoicePreviewAsync(options, planTier, cadence);
        }
        catch (StripeException stripeException)
            when (stripeException.StripeError?.Code == ErrorCodes.InvoiceUpcomingNone)
        {
            // Canceled or suspended: no upcoming invoice, so project the subscription's current items.
            return await invoicePreviewService.GetInvoicePreviewAsync(subscription, planTier, cadence);
        }
    }

    private async Task<(PlanTierType PlanTier, PlanCadenceType Cadence)> ResolveTierAndCadenceAsync(
        ISubscriber subscriber)
    {
        switch (subscriber)
        {
            // Personal Premium is annual-only, matching the cadence GetBitwardenSubscriptionQuery uses today.
            case User:
                return (PlanTierType.Premium, PlanCadenceType.Annually);

            case Organization organization:
                var plan = await pricingClient.GetPlanOrThrow(organization.PlanType);
                var planTier = plan.ProductTier switch
                {
                    ProductTierType.Families => PlanTierType.Families,
                    // TeamsStarter collapses into Teams: the client renders one Teams cart for both.
                    ProductTierType.Teams or ProductTierType.TeamsStarter => PlanTierType.Teams,
                    ProductTierType.Enterprise => PlanTierType.Enterprise,
                    _ => throw new ConflictException(
                        message: $"Organization ({organization.Id}) plan tier ({plan.ProductTier}) has no cart to preview.")
                };
                return (planTier, plan.IsAnnual ? PlanCadenceType.Annually : PlanCadenceType.Monthly);

            default:
                throw new ConflictException(
                    message: $"Cannot build a subscription preview for a {subscriber.SubscriberType()} subscriber.");
        }
    }

    private async Task<Subscription?> FetchSubscriptionAsync(ISubscriber subscriber)
    {
        try
        {
            return await stripeAdapter.GetSubscriptionAsync(subscriber.GatewaySubscriptionId,
                new SubscriptionGetOptions
                {
                    // items.data.price carries the purchasable-reference metadata the no-upcoming-invoice
                    // projection reads; test_clock scopes suspension timing in test scenarios.
                    Expand = ["items.data.price", "test_clock"]
                });
        }
        catch (StripeException stripeException)
            when (stripeException.StripeError?.Code == ErrorCodes.ResourceMissing)
        {
            logger.LogError("Subscription ({SubscriptionId}) for {SubscriberType} ({SubscriberId}) was not found",
                subscriber.GatewaySubscriptionId, subscriber.SubscriberType(), subscriber.Id);
            return null;
        }
    }
}
