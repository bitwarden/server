using Bit.Core.Models.Api;
using Bit.Core.Models.Business;

namespace Bit.Api.Models.Response;

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
