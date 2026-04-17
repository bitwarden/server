using Bit.Core.Models.Business;

namespace Bit.Api.Models.Response;

public class BillingSubscriptionUpcomingInvoice
{
    public BillingSubscriptionUpcomingInvoice(SubscriptionInfo.BillingUpcomingInvoice inv)
    {
        Amount = inv.Amount;
        Date = inv.Date;
    }

    public decimal? Amount { get; set; }
    public DateTime? Date { get; set; }
}
