using Stripe;

namespace Bit.Core.Models.Business;

public class PendingInoviceItems
{
    public IEnumerable<InvoiceItem> PendingInvoiceItems { get; set; }
    public IDictionary<string, InvoiceItem> PendingInvoiceItemsDict { get; set; }
}
