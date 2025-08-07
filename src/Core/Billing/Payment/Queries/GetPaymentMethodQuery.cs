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
using Customer = Stripe.Customer;

namespace Bit.Core.Billing.Payment.Queries;

public interface IGetPaymentMethodQuery
{
    Task<MaskedPaymentMethod?> Run(ISubscriber subscriber);
    string? GetPaymentMethodDescription(Customer customer);
    Task<bool> HasPaymentMethod(Customer customer, Guid? subscriberId = null);
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

    public string? GetPaymentMethodDescription(Customer customer)
    {
        if (customer.Metadata?.ContainsKey(StripeConstants.MetadataKeys.BraintreeCustomerId) == true)
        {
            return "PayPal account";
        }

        if (customer.InvoiceSettings?.DefaultPaymentMethod != null)
        {
            var paymentMethod = customer.InvoiceSettings.DefaultPaymentMethod;
            return paymentMethod.Type switch
            {
                "card" => $"Credit card ending in {paymentMethod.Card?.Last4}",
                "us_bank_account" => $"Bank account ending in {paymentMethod.UsBankAccount?.Last4}",
                _ => "Payment method"
            };
        }

        if (customer.DefaultSource != null)
        {
            return customer.DefaultSource switch
            {
                Card card => $"Credit card ending in {card.Last4}",
                BankAccount bankAccount => $"Bank account ending in {bankAccount.Last4}",
                Source { Card: not null } source => $"Credit card ending in {source.Card.Last4}",
                _ => "Payment method"
            };
        }

        return null;
    }

    public async Task<bool> HasPaymentMethod(Customer customer, Guid? subscriberId = null)
    {
        if (customer.Metadata?.ContainsKey(StripeConstants.MetadataKeys.BraintreeCustomerId) == true)
        {
            return true;
        }

        if (customer.InvoiceSettings?.DefaultPaymentMethod != null)
        {
            return true;
        }

        if (customer.DefaultSource != null)
        {
            return true;
        }

        if (subscriberId.HasValue)
        {
            var setupIntentId = await setupIntentCache.Get(subscriberId.Value);
            if (!string.IsNullOrEmpty(setupIntentId))
            {
                var setupIntent = await stripeAdapter.SetupIntentGet(setupIntentId, new SetupIntentGetOptions
                {
                    Expand = ["payment_method"]
                });

                if (setupIntent.IsUnverifiedBankAccount())
                {
                    return true;
                }
            }
        }

        return false;
    }
}
