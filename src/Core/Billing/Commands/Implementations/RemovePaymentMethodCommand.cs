using Bit.Core.AdminConsole.Entities;
using Bit.Core.Enums;
using Bit.Core.Services;
using Braintree;
using Microsoft.Extensions.Logging;

using static Bit.Core.Billing.Utilities;

namespace Bit.Core.Billing.Commands.Implementations;

public class RemovePaymentMethodCommand(
    IBraintreeGateway braintreeGateway,
    ILogger<RemovePaymentMethodCommand> logger,
    IStripeAdapter stripeAdapter)
    : IRemovePaymentMethodCommand
{
    public async Task RemovePaymentMethod(Organization organization)
    {
        ArgumentNullException.ThrowIfNull(organization);

        if (organization.Gateway is not GatewayType.Stripe || string.IsNullOrEmpty(organization.GatewayCustomerId))
        {
            throw ContactSupport();
        }

        var stripeCustomer = await stripeAdapter.CustomerGetAsync(organization.GatewayCustomerId, new Stripe.CustomerGetOptions
        {
            Expand = ["invoice_settings.default_payment_method", "sources"]
        });

        if (stripeCustomer == null)
        {
            logger.LogError("Could not find Stripe customer ({ID}) when removing payment method", organization.GatewayCustomerId);

            throw ContactSupport();
        }

        if (stripeCustomer.Metadata?.TryGetValue(BraintreeCustomerIdKey, out var braintreeCustomerId) ?? false)
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
        var customer = await braintreeGateway.Customer.FindAsync(braintreeCustomerId);

        if (customer == null)
        {
            logger.LogError("Failed to retrieve Braintree customer ({ID}) when removing payment method", braintreeCustomerId);

            throw ContactSupport();
        }

        if (customer.DefaultPaymentMethod != null)
        {
            var existingDefaultPaymentMethod = customer.DefaultPaymentMethod;

            var updateCustomerResult = await braintreeGateway.Customer.UpdateAsync(
                braintreeCustomerId,
                new CustomerRequest { DefaultPaymentMethodToken = null });

            if (!updateCustomerResult.IsSuccess())
            {
                logger.LogError("Failed to update payment method for Braintree customer ({ID}) | Message: {Message}",
                    braintreeCustomerId, updateCustomerResult.Message);

                throw ContactSupport();
            }

            var deletePaymentMethodResult = await braintreeGateway.PaymentMethod.DeleteAsync(existingDefaultPaymentMethod.Token);

            if (!deletePaymentMethodResult.IsSuccess())
            {
                await braintreeGateway.Customer.UpdateAsync(
                    braintreeCustomerId,
                    new CustomerRequest { DefaultPaymentMethodToken = existingDefaultPaymentMethod.Token });

                logger.LogError(
                    "Failed to delete Braintree payment method for Customer ({ID}), re-linked payment method. Message: {Message}",
                    braintreeCustomerId, deletePaymentMethodResult.Message);

                throw ContactSupport();
            }
        }
        else
        {
            logger.LogWarning("Tried to remove non-existent Braintree payment method for Customer ({ID})", braintreeCustomerId);
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
                        await stripeAdapter.BankAccountDeleteAsync(customer.Id, source.Id);
                        break;
                    case Stripe.Card:
                        await stripeAdapter.CardDeleteAsync(customer.Id, source.Id);
                        break;
                }
            }
        }

        var paymentMethods = stripeAdapter.PaymentMethodListAutoPagingAsync(new Stripe.PaymentMethodListOptions
        {
            Customer = customer.Id
        });

        await foreach (var paymentMethod in paymentMethods)
        {
            await stripeAdapter.PaymentMethodDetachAsync(paymentMethod.Id, new Stripe.PaymentMethodDetachOptions());
        }
    }
}
