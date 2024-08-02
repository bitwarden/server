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
        int pageSize = 20,
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
            var paidInvoicesTask = stripeAdapter.InvoiceListAsync(BuildListOptions("paid"));
            var openInvoicesTask = stripeAdapter.InvoiceListAsync(BuildListOptions("open"));
            var uncollectibleInvoicesTask = stripeAdapter.InvoiceListAsync(BuildListOptions("uncollectible"));

            var paidInvoices = await paidInvoicesTask;
            var openInvoices = await openInvoicesTask;
            var uncollectibleInvoices = await uncollectibleInvoicesTask;

            var invoices = paidInvoices
                .Concat(openInvoices)
                .Concat(uncollectibleInvoices);

            return invoices
                .OrderByDescending(invoice => invoice.Created)
                .Select(invoice => new BillingHistoryInfo.BillingInvoice(invoice))
                .Take(pageSize);

            StripeInvoiceListOptions BuildListOptions(string status) => new()
            {
                Customer = subscriber.GatewayCustomerId,
                Subscription = subscriber.GatewaySubscriptionId,
                Limit = pageSize,
                Status = status,
                StartingAfter = startAfter
            };
        }
        catch (StripeException exception)
        {
            logger.LogError(exception, "An error occurred while listing Stripe invoices");
            throw new GatewayException("Failed to retrieve current invoices", exception);
        }
    }

    public async Task<IEnumerable<BillingHistoryInfo.BillingTransaction>> GetTransactionHistoryAsync(ISubscriber subscriber, int pageSize = 20)
    {
        var transactions = subscriber switch
        {
            User => await transactionRepository.GetManyByUserIdAsync(subscriber.Id, pageSize),
            Organization => await transactionRepository.GetManyByOrganizationIdAsync(subscriber.Id, pageSize),
            _ => null
        };

        return transactions?.OrderByDescending(i => i.CreationDate)
            .Select(t => new BillingHistoryInfo.BillingTransaction(t));
    }
}
