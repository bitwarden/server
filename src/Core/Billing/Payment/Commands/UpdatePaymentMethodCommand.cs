#nullable enable
using Bit.Core.Billing.Caches;
using Bit.Core.Billing.Commands;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Payment.Models;
using Bit.Core.Billing.Services;
using Bit.Core.Entities;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Braintree;
using Microsoft.Extensions.Logging;
using Stripe;
using Customer = Stripe.Customer;

namespace Bit.Core.Billing.Payment.Commands;

public interface IUpdatePaymentMethodCommand
{
    Task<BillingCommandResult<MaskedPaymentMethod>> Run(
        ISubscriber subscriber,
        TokenizedPaymentMethod paymentMethod,
        BillingAddress? billingAddress);
}

public class UpdatePaymentMethodCommand(
    IBraintreeGateway braintreeGateway,
    IGlobalSettings globalSettings,
    ILogger<UpdatePaymentMethodCommand> logger,
    ISetupIntentCache setupIntentCache,
    IStripeAdapter stripeAdapter,
    ISubscriberService subscriberService) : BillingCommand<UpdatePaymentMethodCommand>(logger), IUpdatePaymentMethodCommand
{
    private readonly ILogger<UpdatePaymentMethodCommand> _logger = logger;
    private static readonly Conflict _conflict = new("We had a problem updating your payment method. Please contact support for assistance.");

    public Task<BillingCommandResult<MaskedPaymentMethod>> Run(
        ISubscriber subscriber,
        TokenizedPaymentMethod paymentMethod,
        BillingAddress? billingAddress) => HandleAsync(async () =>
    {
        var customer = await subscriberService.GetCustomer(subscriber);

        var result = paymentMethod.Type switch
        {
            TokenizablePaymentMethodType.BankAccount => await AddBankAccountAsync(subscriber, customer, paymentMethod.Token),
            TokenizablePaymentMethodType.Card => await AddCardAsync(customer, paymentMethod.Token),
            TokenizablePaymentMethodType.PayPal => await AddPayPalAsync(subscriber, customer, paymentMethod.Token),
            _ => new BadRequest($"Payment method type '{paymentMethod.Type}' is not supported.")
        };

        if (billingAddress != null && customer.Address is not { Country: not null, PostalCode: not null })
        {
            await stripeAdapter.CustomerUpdateAsync(customer.Id,
                new CustomerUpdateOptions
                {
                    Address = new AddressOptions
                    {
                        Country = billingAddress.Country,
                        PostalCode = billingAddress.PostalCode
                    }
                });
        }

        return result;
    });

    private async Task<BillingCommandResult<MaskedPaymentMethod>> AddBankAccountAsync(
        ISubscriber subscriber,
        Customer customer,
        string token)
    {
        var setupIntents = await stripeAdapter.SetupIntentList(new SetupIntentListOptions
        {
            Expand = ["data.payment_method"],
            PaymentMethod = token
        });

        switch (setupIntents.Count)
        {
            case 0:
                _logger.LogError("{Command}: Could not find setup intent for subscriber's ({SubscriberID}) bank account", CommandName, subscriber.Id);
                return _conflict;
            case > 1:
                _logger.LogError("{Command}: Found more than one set up intent for subscriber's ({SubscriberID}) bank account", CommandName, subscriber.Id);
                return _conflict;
        }

        var setupIntent = setupIntents.First();

        await setupIntentCache.Set(subscriber.Id, setupIntent.Id);

        await UnlinkBraintreeCustomerAsync(customer);

        return MaskedPaymentMethod.From(setupIntent);
    }

    private async Task<BillingCommandResult<MaskedPaymentMethod>> AddCardAsync(
        Customer customer,
        string token)
    {
        var paymentMethod = await stripeAdapter.PaymentMethodAttachAsync(token, new PaymentMethodAttachOptions { Customer = customer.Id });

        await stripeAdapter.CustomerUpdateAsync(customer.Id,
            new CustomerUpdateOptions
            {
                InvoiceSettings = new CustomerInvoiceSettingsOptions { DefaultPaymentMethod = token }
            });

        await UnlinkBraintreeCustomerAsync(customer);

        return MaskedPaymentMethod.From(paymentMethod.Card);
    }

    private async Task<BillingCommandResult<MaskedPaymentMethod>> AddPayPalAsync(
        ISubscriber subscriber,
        Customer customer,
        string token)
    {
        Braintree.Customer braintreeCustomer;

        if (customer.Metadata.TryGetValue(StripeConstants.MetadataKeys.BraintreeCustomerId, out var braintreeCustomerId))
        {
            braintreeCustomer = await braintreeGateway.Customer.FindAsync(braintreeCustomerId);

            await ReplaceBraintreePaymentMethodAsync(braintreeCustomer, token);
        }
        else
        {
            braintreeCustomer = await CreateBraintreeCustomerAsync(subscriber, token);

            var metadata = new Dictionary<string, string>
            {
                [StripeConstants.MetadataKeys.BraintreeCustomerId] = braintreeCustomer.Id
            };

            await stripeAdapter.CustomerUpdateAsync(customer.Id, new CustomerUpdateOptions { Metadata = metadata });
        }

        var payPalAccount = braintreeCustomer.DefaultPaymentMethod as PayPalAccount;

        return MaskedPaymentMethod.From(payPalAccount!);
    }

    private async Task<Braintree.Customer> CreateBraintreeCustomerAsync(
        ISubscriber subscriber,
        string token)
    {
        var braintreeCustomerId =
            subscriber.BraintreeCustomerIdPrefix() +
            subscriber.Id.ToString("N").ToLower() +
            CoreHelpers.RandomString(3, upper: false, numeric: false);

        var result = await braintreeGateway.Customer.CreateAsync(new CustomerRequest
        {
            Id = braintreeCustomerId,
            CustomFields = new Dictionary<string, string>
            {
                [subscriber.BraintreeIdField()] = subscriber.Id.ToString(),
                [subscriber.BraintreeCloudRegionField()] = globalSettings.BaseServiceUri.CloudRegion
            },
            Email = subscriber.BillingEmailAddress(),
            PaymentMethodNonce = token
        });

        return result.Target;
    }

    private async Task ReplaceBraintreePaymentMethodAsync(
        Braintree.Customer customer,
        string token)
    {
        var existing = customer.DefaultPaymentMethod;

        var result = await braintreeGateway.PaymentMethod.CreateAsync(new PaymentMethodRequest
        {
            CustomerId = customer.Id,
            PaymentMethodNonce = token
        });

        await braintreeGateway.Customer.UpdateAsync(
            customer.Id,
            new CustomerRequest { DefaultPaymentMethodToken = result.Target.Token });

        if (existing != null)
        {
            await braintreeGateway.PaymentMethod.DeleteAsync(existing.Token);
        }
    }

    private async Task UnlinkBraintreeCustomerAsync(
        Customer customer)
    {
        if (customer.Metadata.TryGetValue(StripeConstants.MetadataKeys.BraintreeCustomerId, out var braintreeCustomerId))
        {
            var metadata = new Dictionary<string, string>
            {
                [StripeConstants.MetadataKeys.RetiredBraintreeCustomerId] = braintreeCustomerId,
                [StripeConstants.MetadataKeys.BraintreeCustomerId] = string.Empty
            };

            await stripeAdapter.CustomerUpdateAsync(customer.Id, new CustomerUpdateOptions { Metadata = metadata });
        }
    }
}
