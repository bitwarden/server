using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Services;
using Bit.Core.Billing.Subscriptions.Models;
using Bit.Core.Settings;
using Braintree;
using Braintree.Exceptions;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Services.Implementations;

using static StripeConstants;

public class BraintreeService(
    IBraintreeGateway braintreeGateway,
    IGlobalSettings globalSettings,
    ILogger<BraintreeService> logger,
    IMailService mailService,
    IStripeAdapter stripeAdapter) : IBraintreeService
{
    private readonly Exceptions.ConflictException _problemPayingInvoice = new("There was a problem paying for your invoice. Please contact customer support.");

    public async Task<Customer?> GetCustomer(
        Stripe.Customer customer)
    {
        if (!customer.Metadata.TryGetValue(MetadataKeys.BraintreeCustomerId, out var braintreeCustomerId))
        {
            return null;
        }

        try
        {
            return await braintreeGateway.Customer.FindAsync(braintreeCustomerId);
        }
        catch (NotFoundException)
        {
            logger.LogWarning(
                "Stripe customer ({CustomerId}) is linked to a Braintree Customer ({BraintreeCustomerId}) that does not exist.",
                customer.Id,
                braintreeCustomerId);

            return null;
        }
    }

    public async Task PayInvoice(
        SubscriberId subscriberId,
        Stripe.Invoice invoice)
    {
        if (invoice.Customer == null)
        {
            logger.LogError("Invoice's ({InvoiceID}) `customer` property must be expanded to be paid with Braintree",
                invoice.Id);
            throw _problemPayingInvoice;
        }

        if (!invoice.Customer.Metadata.TryGetValue(MetadataKeys.BraintreeCustomerId, out var braintreeCustomerId))
        {
            logger.LogError(
                "Cannot pay invoice ({InvoiceID}) with Braintree for Customer ({CustomerID}) that does not have a Braintree Customer ID",
                invoice.Id, invoice.Customer.Id);
            throw _problemPayingInvoice;
        }

        if (invoice is not
            {
                AmountDue: > 0,
                Status: not InvoiceStatus.Paid,
                CollectionMethod: CollectionMethod.ChargeAutomatically
            })
        {
            logger.LogWarning("Attempted to pay invoice ({InvoiceID}) with Braintree that is not eligible for payment", invoice.Id);
            return;
        }

        var amount = Math.Round(invoice.AmountDue / 100M, 2);

        var idKey = subscriberId.Match(
            _ => "user_id",
            _ => "organization_id",
            _ => "provider_id");

        var idValue = subscriberId.Match(
            userId => userId.Value,
            organizationId => organizationId.Value,
            providerId => providerId.Value);

        var request = new TransactionRequest
        {
            Amount = amount,
            CustomerId = braintreeCustomerId,
            Options = new TransactionOptionsRequest
            {
                SubmitForSettlement = true,
                PayPal = new TransactionOptionsPayPalRequest
                {
                    CustomField = $"{idKey}:{idValue},region:{globalSettings.BaseServiceUri.CloudRegion}"
                }
            },
            CustomFields = new Dictionary<string, string>
            {
                [idKey] = idValue.ToString(),
                ["region"] = globalSettings.BaseServiceUri.CloudRegion
            }
        };

        var result = await braintreeGateway.Transaction.SaleAsync(request);

        if (!result.IsSuccess())
        {
            if (invoice.AttemptCount < 4)
            {
                await mailService.SendPaymentFailedAsync(invoice.Customer.Email, amount, true);
            }

            return;
        }

        await stripeAdapter.UpdateInvoiceAsync(invoice.Id, new Stripe.InvoiceUpdateOptions
        {
            Metadata = new Dictionary<string, string>
            {
                [MetadataKeys.BraintreeTransactionId] = result.Target.Id,
                [MetadataKeys.PayPalTransactionId] = result.Target.PayPalDetails.AuthorizationId
            }
        });

        await stripeAdapter.PayInvoiceAsync(invoice.Id, new Stripe.InvoicePayOptions { PaidOutOfBand = true });
    }
}
