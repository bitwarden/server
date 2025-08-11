using Bit.Core.Billing.Caches;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Extensions;
using Bit.Core.Billing.Payment.Models;
using Bit.Core.Billing.Services;
using Bit.Core.Entities;
using Bit.Core.Services;
using Braintree;
using Microsoft.Extensions.Logging;
using Stripe;

namespace Bit.Core.Billing.Payment.Queries;

public interface IGetPaymentMethodQuery
{
    Task<MaskedPaymentMethod?> Run(ISubscriber subscriber);
}

public class GetPaymentMethodQuery(
    IBraintreeGateway braintreeGateway,
    ILogger<GetPaymentMethodQuery> logger,
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

        if (customer.Metadata.TryGetValue(StripeConstants.MetadataKeys.BraintreeCustomerId, out var braintreeCustomerId))
        {
            var braintreeCustomer = await braintreeGateway.Customer.FindAsync(braintreeCustomerId);

            if (braintreeCustomer.DefaultPaymentMethod is PayPalAccount payPalAccount)
            {
                return new MaskedPayPalAccount { Email = payPalAccount.Email };
            }

            logger.LogWarning("Subscriber ({SubscriberID}) has a linked Braintree customer ({BraintreeCustomerId}) with no PayPal account.", subscriber.Id, braintreeCustomerId);

            return null;
        }

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

        if (customer.DefaultSource != null)
        {
            paymentMethod = customer.DefaultSource switch
            {
                Card card => MaskedPaymentMethod.From(card),
                BankAccount bankAccount => MaskedPaymentMethod.From(bankAccount),
                Source { Card: not null } source => MaskedPaymentMethod.From(source.Card),
                _ => null
            };

            if (paymentMethod != null)
            {
                return paymentMethod;
            }
        }

        var setupIntentId = await setupIntentCache.Get(subscriber.Id);

        if (string.IsNullOrEmpty(setupIntentId))
        {
            return null;
        }

        var setupIntent = await stripeAdapter.SetupIntentGet(setupIntentId, new SetupIntentGetOptions
        {
            Expand = ["payment_method"]
        });

        // ReSharper disable once ConvertIfStatementToReturnStatement
        if (!setupIntent.IsUnverifiedBankAccount())
        {
            return null;
        }

        return MaskedPaymentMethod.From(setupIntent);
    }

}
