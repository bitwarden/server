using Stripe;

namespace Bit.Core.Models.Business;

public class SubscriptionInfo
{
    public BillingSubscription Subscription { get; set; }
    public BillingUpcomingInvoice UpcomingInvoice { get; set; }
    public bool UsingInAppPurchase { get; set; }

    public class BillingSubscription
    {
        public BillingSubscription(Subscription sub)
        {
            Status = sub.Status;
            TrialStartDate = sub.TrialStart;
            TrialEndDate = sub.TrialEnd;
            PeriodStartDate = sub.CurrentPeriodStart;
            PeriodEndDate = sub.CurrentPeriodEnd;
            CancelledDate = sub.CanceledAt;
            CancelAtEndDate = sub.CancelAtPeriodEnd;
            Cancelled = sub.Status == "canceled" || sub.Status == "unpaid" || sub.Status == "incomplete_expired";
            if (sub.Items?.Data != null)
            {
                Items = sub.Items.Data.Select(i => new BillingSubscriptionItem(i));
            }
        }

        public DateTime? TrialStartDate { get; set; }
        public DateTime? TrialEndDate { get; set; }
        public DateTime? PeriodStartDate { get; set; }
        public DateTime? PeriodEndDate { get; set; }
        public TimeSpan? PeriodDuration => PeriodEndDate - PeriodStartDate;
        public DateTime? CancelledDate { get; set; }
        public bool CancelAtEndDate { get; set; }
        public string Status { get; set; }
        public bool Cancelled { get; set; }
        public IEnumerable<BillingSubscriptionItem> Items { get; set; } = new List<BillingSubscriptionItem>();

        public class BillingSubscriptionItem
        {
            public BillingSubscriptionItem(SubscriptionItem item)
            {
                if (item.Plan != null)
                {
                    Name = item.Plan.Nickname;
                    Amount = item.Plan.Amount.GetValueOrDefault() / 100M;
                    Interval = item.Plan.Interval;
                }

                Quantity = (int)item.Quantity;
                SponsoredSubscriptionItem = Utilities.StaticStore.SponsoredPlans.Any(p => p.StripePlanId == item.Plan.Id);
            }

            public string Name { get; set; }
            public decimal Amount { get; set; }
            public int Quantity { get; set; }
            public string Interval { get; set; }
            public bool SponsoredSubscriptionItem { get; set; }
        }
    }

    public class BillingUpcomingInvoice
    {
        public BillingUpcomingInvoice() { }

        public BillingUpcomingInvoice(Invoice inv)
        {
            Amount = inv.AmountDue / 100M;
            Date = inv.Created;
        }

        public BillingUpcomingInvoice(Braintree.Subscription sub)
        {
            Amount = sub.NextBillAmount.GetValueOrDefault() + sub.Balance.GetValueOrDefault();
            if (Amount < 0)
            {
                Amount = 0;
            }
            Date = sub.NextBillingDate;
        }

        public decimal Amount { get; set; }
        public DateTime? Date { get; set; }
    }
}
