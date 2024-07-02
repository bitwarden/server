using Bit.Billing.Constants;
using Stripe;
using Event = Stripe.Event;

namespace Bit.Billing.Services.Implementations;

public class PaymentMethodAttachedHandler : IPaymentMethodAttachedHandler
{
    private readonly ILogger<PaymentMethodAttachedHandler> _logger;
    private readonly IStripeEventService _stripeEventService;
    private readonly IStripeFacade _stripeFacade;
    private readonly IStripeEventUtilityService _stripeEventUtilityService;

    public PaymentMethodAttachedHandler(
        ILogger<PaymentMethodAttachedHandler> logger,
        IStripeEventService stripeEventService,
        IStripeFacade stripeFacade,
        IStripeEventUtilityService stripeEventUtilityService)
    {
        _logger = logger;
        _stripeEventService = stripeEventService;
        _stripeFacade = stripeFacade;
        _stripeEventUtilityService = stripeEventUtilityService;
    }

    public async Task HandleAsync(Event parsedEvent)
    {
        var paymentMethod = await _stripeEventService.GetPaymentMethod(parsedEvent);
        if (paymentMethod is null)
        {
            _logger.LogWarning("Attempted to handle the event payment_method.attached but paymentMethod was null");
            return;
        }

        var subscriptionListOptions = new SubscriptionListOptions
        {
            Customer = paymentMethod.CustomerId,
            Status = StripeSubscriptionStatus.Unpaid,
            Expand = ["data.latest_invoice"]
        };

        StripeList<Subscription> unpaidSubscriptions;
        try
        {
            unpaidSubscriptions = await _stripeFacade.ListSubscriptions(subscriptionListOptions);
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
            await _stripeEventUtilityService.AttemptToPayInvoiceAsync(latestInvoice, true);
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
