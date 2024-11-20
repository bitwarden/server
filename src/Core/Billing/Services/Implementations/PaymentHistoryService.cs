#nullable enable
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Models;
using Bit.Core.Entities;
using Bit.Core.Models.BitStripe;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Billing.Services.Implementations;

public class PaymentHistoryService(
    IStripeAdapter stripeAdapter,
    ITransactionRepository transactionRepository,
    ILogger<PaymentHistoryService> logger) : IPaymentHistoryService
{
    public async Task<IEnumerable<BillingHistoryInfo.BillingInvoice>> GetInvoiceHistoryAsync(
        ISubscriber subscriber,
        int pageSize = 5,
        string? status = null,
        string? startAfter = null)
    {
        if (subscriber is not { GatewayCustomerId: not null, GatewaySubscriptionId: not null })
        {
            return Array.Empty<BillingHistoryInfo.BillingInvoice>();
        }

        var invoices = await stripeAdapter.InvoiceListAsync(new StripeInvoiceListOptions
        {
            Customer = subscriber.GatewayCustomerId,
            Subscription = subscriber.GatewaySubscriptionId,
            Limit = pageSize,
            Status = status,
            StartingAfter = startAfter
        });

        return invoices.Select(invoice => new BillingHistoryInfo.BillingInvoice(invoice));

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
            .Select(t => new BillingHistoryInfo.BillingTransaction(t))
            ?? Array.Empty<BillingHistoryInfo.BillingTransaction>();
    }
}
