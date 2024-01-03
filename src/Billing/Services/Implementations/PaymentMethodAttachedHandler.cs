using Bit.Billing.Constants;
using Microsoft.AspNetCore.Mvc;
using Stripe;

namespace Bit.Billing.Services.Implementations;

public class PaymentMethodAttachedHandler : StripeWebhookHandler
{
    private readonly IStripeEventService _stripeEventService;
    private readonly IWebhookUtility _webhookUtility;
    private readonly ILogger<PaymentMethodAttachedHandler> _logger;

    public PaymentMethodAttachedHandler(IStripeEventService stripeEventService,
        IWebhookUtility webhookUtility,
        ILogger<PaymentMethodAttachedHandler> logger)
    {
        _stripeEventService = stripeEventService;
        _webhookUtility = webhookUtility;
        _logger = logger;
    }

    protected override bool CanHandle(Event parsedEvent)
    {
        return parsedEvent.Type.Equals(HandledStripeWebhook.InvoiceCreated);
    }

    protected override async Task<IActionResult> ProcessEvent(Event parsedEvent)
    {
        var paymentMethod = await _stripeEventService.GetPaymentMethod(parsedEvent);
        await HandlePaymentMethodAttachedAsync(paymentMethod);
        return new OkResult();
    }

    private async Task HandlePaymentMethodAttachedAsync(PaymentMethod paymentMethod)
    {
        if (paymentMethod is null)
        {
            _logger.LogWarning("Attempted to handle the event payment_method.attached but paymentMethod was null");
            return;
        }

        var subscriptionService = new SubscriptionService();
        var subscriptionListOptions = new SubscriptionListOptions
        {
            Customer = paymentMethod.CustomerId,
            Status = StripeSubscriptionStatus.Unpaid,
            Expand = new List<string> { "data.latest_invoice" }
        };

        StripeList<Subscription> unpaidSubscriptions;
        try
        {
            unpaidSubscriptions = await subscriptionService.ListAsync(subscriptionListOptions);
        }
        catch (Exception e)
        {
            _logger.LogError(e,
                "Attempted to get unpaid invoices for customer {CustomerId} but encountered an error while calling Stripe",
                paymentMethod.CustomerId);

            return;
        }

        foreach (var unpaidSubscription in unpaidSubscriptions)
        {
            await AttemptToPayOpenSubscriptionAsync(unpaidSubscription);
        }
    }

    private async Task AttemptToPayOpenSubscriptionAsync(Subscription unpaidSubscription)
    {
        var latestInvoice = unpaidSubscription.LatestInvoice;

        if (unpaidSubscription.LatestInvoice is null)
        {
            _logger.LogWarning(
                "Attempted to pay unpaid subscription {SubscriptionId} but latest invoice didn't exist",
                unpaidSubscription.Id);

            return;
        }

        if (latestInvoice.Status != StripeInvoiceStatus.Open)
        {
            _logger.LogWarning(
                "Attempted to pay unpaid subscription {SubscriptionId} but latest invoice wasn't \"open\"",
                unpaidSubscription.Id);

            return;
        }

        try
        {
            await _webhookUtility.AttemptToPayInvoice(latestInvoice, true);
        }
        catch (Exception e)
        {
            _logger.LogError(e,
                "Attempted to pay open invoice {InvoiceId} on unpaid subscription {SubscriptionId} but encountered an error",
                latestInvoice.Id, unpaidSubscription.Id);
            throw;
        }
    }

}
