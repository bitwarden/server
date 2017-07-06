using Stripe;
using System.Collections.Generic;

namespace Bit.Core.Models.Business
{
    public class BillingInfo
    {
        public Source PaymentSource { get; set; }
        public StripeSubscription Subscription { get; set; }
        public StripeInvoice UpcomingInvoice { get; set; }
        public IEnumerable<StripeCharge> Charges { get; set; } = new List<StripeCharge>();
    }
}
