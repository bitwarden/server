// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Billing.Constants;
using Bit.Core;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Extensions;
using Bit.Core.Services;
using Stripe;
using Event = Stripe.Event;

namespace Bit.Billing.Services.Implementations;

public class PaymentMethodAttachedHandler : IPaymentMethodAttachedHandler
{
    private readonly ILogger<PaymentMethodAttachedHandler> _logger;
    private readonly IStripeEventService _stripeEventService;
    private readonly IStripeFacade _stripeFacade;
    private readonly IStripeEventUtilityService _stripeEventUtilityService;
    private readonly IFeatureService _featureService;
    private readonly IProviderRepository _providerRepository;

    public PaymentMethodAttachedHandler(
        ILogger<PaymentMethodAttachedHandler> logger,
        IStripeEventService stripeEventService,
        IStripeFacade stripeFacade,
        IStripeEventUtilityService stripeEventUtilityService,
        IFeatureService featureService,
        IProviderRepository providerRepository)
    {
        _logger = logger;
        _stripeEventService = stripeEventService;
        _stripeFacade = stripeFacade;
        _stripeEventUtilityService = stripeEventUtilityService;
        _featureService = featureService;
        _providerRepository = providerRepository;
    }

    public async Task HandleAsync(Event parsedEvent)
    {
        var updateMSPToChargeAutomatically =
            _featureService.IsEnabled(FeatureFlagKeys.PM199566_UpdateMSPToChargeAutomatically);

        if (updateMSPToChargeAutomatically)
        {
            await HandleVNextAsync(parsedEvent);
        }
        else
        {
            await HandleVCurrentAsync(parsedEvent);
        }
    }

    private async Task HandleVNextAsync(Event parsedEvent)
    {
        var paymentMethod = await _stripeEventService.GetPaymentMethod(parsedEvent, true, ["customer.subscriptions.data.latest_invoice"]);

        if (paymentMethod == null)
        {
            _logger.LogWarning("Attempted to handle the event payment_method.attached but paymentMethod was null");
            return;
        }

        var customer = paymentMethod.Customer;
        var subscriptions = customer?.Subscriptions;

        // This represents a provider subscription set to "send_invoice" that was paid using a Stripe hosted invoice payment page.
        var invoicedProviderSubscription = subscriptions?.Data.FirstOrDefault(subscription =>
            subscription.Metadata.ContainsKey(StripeConstants.MetadataKeys.ProviderId) &&
            subscription.Status != StripeConstants.SubscriptionStatus.Canceled &&
            subscription.CollectionMethod == StripeConstants.CollectionMethod.SendInvoice);

        /*
         * If we have an invoiced provider subscription where the customer hasn't been marked as invoice-approved,
         * we need to try and set the default payment method and update the collection method to be "charge_automatically".
         */
        if (invoicedProviderSubscription != null &&
            !customer.ApprovedToPayByInvoice() &&
            Guid.TryParse(invoicedProviderSubscription.Metadata[StripeConstants.MetadataKeys.ProviderId], out var providerId))
        {
            var provider = await _providerRepository.GetByIdAsync(providerId);

            if (provider is { Type: ProviderType.Msp })
            {
                if (customer.InvoiceSettings.DefaultPaymentMethodId != paymentMethod.Id)
                {
                    try
                    {
                        await _stripeFacade.UpdateCustomer(customer.Id,
                            new CustomerUpdateOptions
                            {
                                InvoiceSettings = new CustomerInvoiceSettingsOptions
                                {
                                    DefaultPaymentMethod = paymentMethod.Id
                                }
                            });
                    }
                    catch (Exception exception)
                    {
                        _logger.LogWarning(exception,
                            "Failed to set customer's ({CustomerID}) default payment method during 'payment_method.attached' webhook",
                            customer.Id);
                    }
                }

                try
                {
                    await _stripeFacade.UpdateSubscription(invoicedProviderSubscription.Id,
                        new SubscriptionUpdateOptions
                        {
                            CollectionMethod = StripeConstants.CollectionMethod.ChargeAutomatically
                        });
                }
                catch (Exception exception)
                {
                    _logger.LogWarning(exception,
                        "Failed to set subscription's ({SubscriptionID}) collection method to 'charge_automatically' during 'payment_method.attached' webhook",
                        customer.Id);
                }
            }
        }

        var unpaidSubscriptions = subscriptions?.Data.Where(subscription =>
            subscription.Status == StripeConstants.SubscriptionStatus.Unpaid).ToList();

        if (unpaidSubscriptions == null || unpaidSubscriptions.Count == 0)
        {
            return;
        }

        foreach (var unpaidSubscription in unpaidSubscriptions)
        {
            await AttemptToPayOpenSubscriptionAsync(unpaidSubscription);
        }
    }

    private async Task HandleVCurrentAsync(Event parsedEvent)
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
