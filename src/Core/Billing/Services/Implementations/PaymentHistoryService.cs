using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Models;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Models.BitStripe;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Microsoft.Extensions.Logging;
using Stripe;

namespace Bit.Core.Billing.Services.Implementations;

public class PaymentHistoryService(
    IStripeAdapter stripeAdapter,
    ITransactionRepository transactionRepository,
    ILogger<PaymentHistoryService> logger) : IPaymentHistoryService
{
    public async Task<IEnumerable<BillingHistoryInfo.BillingInvoice>> GetInvoiceHistoryAsync(
        ISubscriber subscriber,
        int pageSize = 5,
        string startAfter = null)
    {
        if (subscriber is null ||
            string.IsNullOrEmpty(subscriber.GatewayCustomerId) ||
            string.IsNullOrEmpty(subscriber.GatewaySubscriptionId))
        {
            return null;
        }

        try
        {
            var invoices = await stripeAdapter.InvoiceListAsync(new StripeInvoiceListOptions
            {
                Customer = subscriber.GatewayCustomerId,
                Subscription = subscriber.GatewaySubscriptionId,
                Limit = pageSize,
                StartingAfter = startAfter
            });

            return invoices.Select(invoice => new BillingHistoryInfo.BillingInvoice(invoice));
        }
        catch (StripeException exception)
        {
            logger.LogError(exception, "An error occurred while listing Stripe invoices");
            throw new GatewayException("Failed to retrieve current invoices", exception);
        }
    }

    public async Task<IEnumerable<BillingHistoryInfo.BillingTransaction>> GetTransactionHistoryAsync(
        ISubscriber subscriber,
        int pageSize = 5,
        DateTime? startAfter = null)
    {
        var transactions = subscriber switch
        {
            User => await transactionRepository.GetManyByUserIdAsync(subscriber.Id, pageSize, startAfter),
            Organization => await transactionRepository.GetManyByOrganizationIdAsync(subscriber.Id, pageSize, startAfter),
            _ => null
        };

        return transactions?.OrderByDescending(i => i.CreationDate)
            .Select(t => new BillingHistoryInfo.BillingTransaction(t));
    }
}
