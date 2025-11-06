using Bit.Core.Billing.Extensions;
using Stripe;

#nullable enable

namespace Bit.Core.Models.Business;

public class SubscriptionInfo
{
    /// <summary>
    /// Converts Stripe's minor currency units (cents) to major currency units (dollars).
    /// Stripe stores monetary amounts in the smallest currency unit (e.g., cents for USD).
    /// </summary>
    private const decimal StripeMinorUnitDivisor = 100M;

    public BillingCustomerDiscount? CustomerDiscount { get; set; }
    public BillingSubscription? Subscription { get; set; }
    public BillingUpcomingInvoice? UpcomingInvoice { get; set; }

    /// <summary>
    /// Represents customer discount information from Stripe billing.
    /// </summary>
    public class BillingCustomerDiscount
    {
        public BillingCustomerDiscount() { }

        /// <summary>
        /// Creates a BillingCustomerDiscount from a Stripe Discount object.
        /// </summary>
        /// <param name="discount">The Stripe discount containing coupon and expiration information.</param>
        public BillingCustomerDiscount(Discount discount)
        {
            Id = discount.Coupon?.Id;
            Active = discount.End == null;
            PercentOff = discount.Coupon?.PercentOff;
            AmountOff = discount.Coupon?.AmountOff / StripeMinorUnitDivisor;
            AppliesTo = discount.Coupon?.AppliesTo?.Products;
        }

        /// <summary>
        /// The Stripe coupon ID (e.g., "cm3nHfO1").
        /// </summary>
        public string? Id { get; set; }

        /// <summary>
        /// Whether the discount is currently active.
        /// A discount is considered active when it has no end date (discount.End == null),
        /// meaning it applies indefinitely to future renewals.
        /// <para>
        /// Note: This does not distinguish between future expiration dates (still active, will expire)
        /// and past expiration dates (already expired). Any discount with an End date set
        /// (regardless of whether it's in the future or past) is considered inactive.
        /// This is intentional for Milestone 2 implementation.
        /// </para>
        /// </summary>
        public bool Active { get; set; }

        /// <summary>
        /// Percentage discount applied to the subscription (e.g., 20.0 for 20% off).
        /// Null if this is an amount-based discount.
        /// </summary>
        public decimal? PercentOff { get; set; }

        /// <summary>
        /// Fixed amount discount in USD (e.g., 14.00 for $14 off).
        /// Converted from Stripe's cent-based values (1400 cents → $14.00).
        /// Null if this is a percentage-based discount.
        /// </summary>
        public decimal? AmountOff { get; set; }

        /// <summary>
        /// List of Stripe product IDs that this discount applies to (e.g., ["prod_premium", "prod_families"]).
        /// Null indicates the discount applies to all products with no restrictions.
        /// Empty list indicates a discount restricted to zero products (edge case).
        /// </summary>
        public List<string>? AppliesTo { get; set; }
    }

    public class BillingSubscription
    {
        public BillingSubscription(Subscription sub)
        {
            Status = sub.Status;
            TrialStartDate = sub.TrialStart;
            TrialEndDate = sub.TrialEnd;
            var currentPeriod = sub.GetCurrentPeriod();
            if (currentPeriod != null)
            {
                var (start, end) = currentPeriod.Value;
                PeriodStartDate = start;
                PeriodEndDate = end;
            }
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
        public string? Status { get; set; }
        public bool Cancelled { get; set; }
        public IEnumerable<BillingSubscriptionItem> Items { get; set; } = new List<BillingSubscriptionItem>();
        public string? CollectionMethod { get; set; }
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
                    Amount = item.Plan.Amount.GetValueOrDefault() / StripeMinorUnitDivisor;
                    Interval = item.Plan.Interval;

                    if (item.Metadata != null)
                    {
                        AddonSubscriptionItem = item.Metadata.TryGetValue("isAddOn", out var value) && bool.Parse(value);
                    }
                }

                Quantity = (int)item.Quantity;
                SponsoredSubscriptionItem = item.Plan != null && Utilities.StaticStore.SponsoredPlans.Any(p => p.StripePlanId == item.Plan.Id);
            }

            public bool AddonSubscriptionItem { get; set; }
            public string? ProductId { get; set; }
            public string? Name { get; set; }
            public decimal Amount { get; set; }
            public int Quantity { get; set; }
            public string? Interval { get; set; }
            public bool SponsoredSubscriptionItem { get; set; }
        }
    }

    public class BillingUpcomingInvoice
    {
        public BillingUpcomingInvoice() { }

        public BillingUpcomingInvoice(Invoice inv)
        {
            Amount = inv.AmountDue / StripeMinorUnitDivisor;
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
