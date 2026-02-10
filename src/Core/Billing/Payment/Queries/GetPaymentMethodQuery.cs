using Bit.Core.Billing.Caches;
using Bit.Core.Billing.Extensions;
using Bit.Core.Billing.Payment.Models;
using Bit.Core.Billing.Services;
using Bit.Core.Entities;
using Bit.Core.Services;
using Braintree;
using Stripe;

namespace Bit.Core.Billing.Payment.Queries;

public interface IGetPaymentMethodQuery
{
    Task<MaskedPaymentMethod?> Run(ISubscriber subscriber);
}

public class GetPaymentMethodQuery(
    IBraintreeService braintreeService,
    ISetupIntentCache setupIntentCache,
    IStripeAdapter stripeAdapter,
    ISubscriberService subscriberService) : IGetPaymentMethodQuery
{
    public async Task<MaskedPaymentMethod?> Run(ISubscriber subscriber)
    {
        var customer = await subscriberService.GetCustomer(subscriber,
            new CustomerGetOptions { Expand = ["default_source", "invoice_settings.default_payment_method"] });

        if (customer == null)
        {
            return null;
        }

        // First check for a PayPal account
        var braintreeCustomer = await braintreeService.GetCustomer(customer);

        if (braintreeCustomer is { DefaultPaymentMethod: PayPalAccount payPalAccount })
        {
            return new MaskedPayPalAccount { Email = payPalAccount.Email };
        }

        // Then check for a bank account pending verification
        var setupIntentId = await setupIntentCache.GetSetupIntentIdForSubscriber(subscriber.Id);

        if (!string.IsNullOrEmpty(setupIntentId))
        {
            var setupIntent = await stripeAdapter.GetSetupIntentAsync(setupIntentId, new SetupIntentGetOptions
            {
                Expand = ["payment_method"]
            });

            if (setupIntent.IsUnverifiedBankAccount())
            {
                return MaskedPaymentMethod.From(setupIntent);
            }
        }

        // Then check the default payment method
        var paymentMethod = customer.InvoiceSettings.DefaultPaymentMethod != null
            ? customer.InvoiceSettings.DefaultPaymentMethod.Type switch
            {
                "card" => MaskedPaymentMethod.From(customer.InvoiceSettings.DefaultPaymentMethod.Card),
                "us_bank_account" => MaskedPaymentMethod.From(customer.InvoiceSettings.DefaultPaymentMethod.UsBankAccount),
                _ => null
            }
            : null;

        if (paymentMethod != null)
        {
            return paymentMethod;
        }

        return customer.DefaultSource switch
        {
            Card card => MaskedPaymentMethod.From(card),
            BankAccount bankAccount => MaskedPaymentMethod.From(bankAccount),
            Source { Card: not null } source => MaskedPaymentMethod.From(source.Card),
            _ => null
        };
    }
}
