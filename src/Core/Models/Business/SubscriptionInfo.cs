﻿using Stripe;

namespace Bit.Core.Models.Business;

public class SubscriptionInfo
{
    public BillingCustomerDiscount CustomerDiscount { get; set; }
    public BillingSubscription Subscription { get; set; }
    public BillingUpcomingInvoice UpcomingInvoice { get; set; }

    public class BillingCustomerDiscount
    {
        public BillingCustomerDiscount() { }

        public BillingCustomerDiscount(Discount discount)
        {
            Id = discount.Coupon?.Id;
            Active = discount.End == null;
            PercentOff = discount.Coupon?.PercentOff;
            AppliesTo = discount.Coupon?.AppliesTo?.Products ?? [];
        }

        public string Id { get; set; }
        public bool Active { get; set; }
        public decimal? PercentOff { get; set; }
        public List<string> AppliesTo { get; set; }
    }

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
            CollectionMethod = sub.CollectionMethod;
            GracePeriod = sub.CollectionMethod == "charge_automatically"
                ? 14
                : 30;
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
        public string CollectionMethod { get; set; }
        public DateTime? SuspensionDate { get; set; }
        public DateTime? UnpaidPeriodEndDate { get; set; }
        public int GracePeriod { get; set; }

        public class BillingSubscriptionItem
        {
            public BillingSubscriptionItem(SubscriptionItem item)
            {
                if (item.Plan != null)
                {
                    ProductId = item.Plan.ProductId;
                    Name = item.Plan.Nickname;
                    Amount = item.Plan.Amount.GetValueOrDefault() / 100M;
                    Interval = item.Plan.Interval;

                    if (item.Metadata != null)
                    {
                        AddonSubscriptionItem = item.Metadata.TryGetValue("isAddOn", out var value) && bool.Parse(value);
                    }
                }

                Quantity = (int)item.Quantity;
                SponsoredSubscriptionItem = Utilities.StaticStore.SponsoredPlans.Any(p => p.StripePlanId == item.Plan.Id);
            }

            public bool AddonSubscriptionItem { get; set; }
            public string ProductId { get; set; }
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
