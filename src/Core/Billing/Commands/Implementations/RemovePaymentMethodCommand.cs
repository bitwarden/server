using Bit.Core.AdminConsole.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Services;
using Braintree;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Billing.Commands.Implementations;

public class RemovePaymentMethodCommand : IRemovePaymentMethodCommand
{
    private readonly IBraintreeGateway _braintreeGateway;
    private readonly ILogger<RemovePaymentMethodCommand> _logger;
    private readonly IStripeAdapter _stripeAdapter;

    public RemovePaymentMethodCommand(
        IBraintreeGateway braintreeGateway,
        ILogger<RemovePaymentMethodCommand> logger,
        IStripeAdapter stripeAdapter)
    {
        _braintreeGateway = braintreeGateway;
        _logger = logger;
        _stripeAdapter = stripeAdapter;
    }

    public async Task RemovePaymentMethod(Organization organization)
    {
        const string braintreeCustomerIdKey = "btCustomerId";

        if (organization == null)
        {
            throw new ArgumentNullException(nameof(organization));
        }

        if (organization.Gateway is not GatewayType.Stripe || string.IsNullOrEmpty(organization.GatewayCustomerId))
        {
            throw ContactSupport();
        }

        var stripeCustomer = await _stripeAdapter.CustomerGetAsync(organization.GatewayCustomerId, new Stripe.CustomerGetOptions
        {
            Expand = new List<string> { "invoice_settings.default_payment_method", "sources" }
        });

        if (stripeCustomer == null)
        {
            _logger.LogError("Could not find Stripe customer ({ID}) when removing payment method", organization.GatewayCustomerId);

            throw ContactSupport();
        }

        if (stripeCustomer.Metadata?.TryGetValue(braintreeCustomerIdKey, out var braintreeCustomerId) ?? false)
        {
            await RemoveBraintreePaymentMethodAsync(braintreeCustomerId);
        }
        else
        {
            await RemoveStripePaymentMethodsAsync(stripeCustomer);
        }
    }

    private async Task RemoveBraintreePaymentMethodAsync(string braintreeCustomerId)
    {
        var customer = await _braintreeGateway.Customer.FindAsync(braintreeCustomerId);

        if (customer == null)
        {
            _logger.LogError("Failed to retrieve Braintree customer ({ID}) when removing payment method", braintreeCustomerId);

            throw ContactSupport();
        }

        if (customer.DefaultPaymentMethod != null)
        {
            var existingDefaultPaymentMethod = customer.DefaultPaymentMethod;

            var updateCustomerResult = await _braintreeGateway.Customer.UpdateAsync(
                braintreeCustomerId,
                new CustomerRequest { DefaultPaymentMethodToken = null });

            if (!updateCustomerResult.IsSuccess())
            {
                _logger.LogError("Failed to update payment method for Braintree customer ({ID}) | Message: {Message}",
                    braintreeCustomerId, updateCustomerResult.Message);

                throw ContactSupport();
            }

            var deletePaymentMethodResult = await _braintreeGateway.PaymentMethod.DeleteAsync(existingDefaultPaymentMethod.Token);

            if (!deletePaymentMethodResult.IsSuccess())
            {
                await _braintreeGateway.Customer.UpdateAsync(
                    braintreeCustomerId,
                    new CustomerRequest { DefaultPaymentMethodToken = existingDefaultPaymentMethod.Token });

                _logger.LogError(
                    "Failed to delete Braintree payment method for Customer ({ID}), re-linked payment method. Message: {Message}",
                    braintreeCustomerId, deletePaymentMethodResult.Message);

                throw ContactSupport();
            }
        }
        else
        {
            _logger.LogWarning("Tried to remove non-existent Braintree payment method for Customer ({ID})", braintreeCustomerId);
        }
    }

    private async Task RemoveStripePaymentMethodsAsync(Stripe.Customer customer)
    {
        if (customer.Sources != null && customer.Sources.Any())
        {
            foreach (var source in customer.Sources)
            {
                switch (source)
                {
                    case Stripe.BankAccount:
                        await _stripeAdapter.BankAccountDeleteAsync(customer.Id, source.Id);
                        break;
                    case Stripe.Card:
                        await _stripeAdapter.CardDeleteAsync(customer.Id, source.Id);
                        break;
                }
            }
        }

        var paymentMethods = _stripeAdapter.PaymentMethodListAutoPagingAsync(new Stripe.PaymentMethodListOptions
        {
            Customer = customer.Id
        });

        await foreach (var paymentMethod in paymentMethods)
        {
            await _stripeAdapter.PaymentMethodDetachAsync(paymentMethod.Id, new Stripe.PaymentMethodDetachOptions());
        }
    }

    private static GatewayException ContactSupport() => new("Could not remove your payment method. Please contact support for assistance.");
}
