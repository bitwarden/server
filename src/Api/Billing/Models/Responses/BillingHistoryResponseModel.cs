using Bit.Core.Billing.Models;
using Bit.Core.Models.Api;

namespace Bit.Api.Billing.Models.Responses;

public class BillingHistoryResponseModel : ResponseModel
{
    public BillingHistoryResponseModel(BillingInfo billing)
        : base("billingHistory")
    {
        Transactions = billing.Transactions?.Select(t => new BillingTransaction(t));
        Invoices = billing.Invoices?.Select(i => new BillingInvoice(i));
    }
    public IEnumerable<BillingInvoice> Invoices { get; set; }
    public IEnumerable<BillingTransaction> Transactions { get; set; }
}
