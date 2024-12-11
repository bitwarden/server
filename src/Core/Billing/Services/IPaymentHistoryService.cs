#nullable enable
using Bit.Core.Billing.Models;
using Bit.Core.Entities;

namespace Bit.Core.Billing.Services;

public interface IPaymentHistoryService
{
    Task<IEnumerable<BillingHistoryInfo.BillingInvoice>> GetInvoiceHistoryAsync(
        ISubscriber subscriber,
        int pageSize = 5,
        string? status = null,
        string? startAfter = null
    );

    Task<IEnumerable<BillingHistoryInfo.BillingTransaction>> GetTransactionHistoryAsync(
        ISubscriber subscriber,
        int pageSize = 5,
        DateTime? startAfter = null
    );
}
