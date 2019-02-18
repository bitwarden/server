using Stripe;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Bit.Core.Models.Business
{
    public class SubscriptionInfo
    {
        public BillingSubscription Subscription { get; set; }
        public BillingUpcomingInvoice UpcomingInvoice { get; set; }

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
                Cancelled = sub.Status == "canceled" || sub.Status == "unpaid";
                if(sub.Items?.Data != null)
                {
                    Items = sub.Items.Data.Select(i => new BillingSubscriptionItem(i));
                }
            }

            public BillingSubscription(Braintree.Subscription sub, Braintree.Plan plan)
            {
                Status = sub.Status.ToString();

                if(sub.HasTrialPeriod.GetValueOrDefault() && sub.CreatedAt.HasValue && sub.TrialDuration.HasValue)
                {
                    TrialStartDate = sub.CreatedAt.Value;
                    if(sub.TrialDurationUnit == Braintree.SubscriptionDurationUnit.DAY)
                    {
                        TrialEndDate = TrialStartDate.Value.AddDays(sub.TrialDuration.Value);
                    }
                    else
                    {
                        TrialEndDate = TrialStartDate.Value.AddMonths(sub.TrialDuration.Value);
                    }
                }

                PeriodStartDate = sub.BillingPeriodStartDate;
                PeriodEndDate = sub.BillingPeriodEndDate;

                CancelAtEndDate = !sub.NeverExpires.GetValueOrDefault();
                Cancelled = sub.Status == Braintree.SubscriptionStatus.CANCELED;
                if(Cancelled)
                {
                    CancelledDate = sub.UpdatedAt.Value;
                }

                var items = new List<BillingSubscriptionItem>();
                items.Add(new BillingSubscriptionItem(plan));
                if(sub.AddOns != null)
                {
                    items.AddRange(sub.AddOns.Select(a => new BillingSubscriptionItem(plan, a)));
                }

                if(items.Count > 0)
                {
                    Items = items;
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
                    if(item.Plan != null)
                    {
                        Name = item.Plan.Nickname;
                        Amount = item.Plan.Amount.GetValueOrDefault() / 100M;
                        Interval = item.Plan.Interval;
                    }

                    Quantity = (int)item.Quantity;
                }

                public BillingSubscriptionItem(Braintree.Plan plan)
                {
                    Name = plan.Name;
                    Amount = plan.Price.GetValueOrDefault();
                    Interval = plan.BillingFrequency.GetValueOrDefault() == 12 ? "year" : "month";
                    Quantity = 1;
                }

                public BillingSubscriptionItem(Braintree.Plan plan, Braintree.AddOn addon)
                {
                    Name = addon.Name;
                    Amount = addon.Amount.GetValueOrDefault();
                    Interval = plan.BillingFrequency.GetValueOrDefault() == 12 ? "year" : "month";
                    Quantity = addon.Quantity.GetValueOrDefault();
                }

                public string Name { get; set; }
                public decimal Amount { get; set; }
                public int Quantity { get; set; }
                public string Interval { get; set; }
            }
        }

        public class BillingUpcomingInvoice
        {
            public BillingUpcomingInvoice() { }

            public BillingUpcomingInvoice(Invoice inv)
            {
                Amount = inv.AmountDue / 100M;
                Date = inv.Date.Value;
            }

            public BillingUpcomingInvoice(Braintree.Subscription sub)
            {
                Amount = sub.NextBillAmount.GetValueOrDefault() + sub.Balance.GetValueOrDefault();
                if(Amount < 0)
                {
                    Amount = 0;
                }
                Date = sub.NextBillingDate;
            }

            public decimal Amount { get; set; }
            public DateTime? Date { get; set; }
        }
    }
}
