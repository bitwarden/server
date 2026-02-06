using Bit.Core.Billing.Extensions;
using Bit.Core.Billing.Models;
using Stripe;

#nullable enable

namespace Bit.Core.Models.Business;

public class SubscriptionInfo
{
    /// <summary>
    /// Converts Stripe's minor currency units (cents) to major currency units (dollars).
    /// IMPORTANT: Only supports USD. All Bitwarden subscriptions are USD-only.
    /// </summary>
    private const decimal StripeMinorUnitDivisor = 100M;

    /// <summary>
    /// Converts Stripe's minor currency units (cents) to major currency units (dollars).
    /// Preserves null semantics to distinguish between "no amount" (null) and "zero amount" (0.00m).
    /// </summary>
    /// <param name="amountInCents">The amount in Stripe's minor currency units (e.g., cents for USD).</param>
    /// <returns>The amount in major currency units (e.g., dollars for USD), or null if the input is null.</returns>
    private static decimal? ConvertFromStripeMinorUnits(long? amountInCents)
    {
        return amountInCents.HasValue ? amountInCents.Value / StripeMinorUnitDivisor : null;
    }

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
            // Active = true only for perpetual/recurring discounts (no end date)
            // This is intentional for Milestone 2 - only perpetual discounts are shown in UI
            Active = discount.End == null;
            PercentOff = discount.Coupon?.PercentOff;
            AmountOff = ConvertFromStripeMinorUnits(discount.Coupon?.AmountOff);
            // Stripe's CouponAppliesTo.Products is already IReadOnlyList<string>, so no conversion needed
            AppliesTo = discount.Coupon?.AppliesTo?.Products;
        }

        /// <summary>
        /// The Stripe coupon ID (e.g., "cm3nHfO1").
        /// Note: Only specific coupon IDs are displayed in the UI based on feature flag configuration,
        /// though Stripe may apply additional discounts that are not shown.
        /// </summary>
        public string? Id { get; set; }

        /// <summary>
        /// True only for perpetual/recurring discounts (End == null).
        /// False for any discount with an expiration date, even if not yet expired.
        /// Product decision for Milestone 2: only show perpetual discounts in UI.
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
        /// <para>
        /// Null: discount applies to all products with no restrictions (AppliesTo not specified in Stripe).
        /// Empty list: discount restricted to zero products (edge case - AppliesTo.Products = [] in Stripe).
        /// Non-empty list: discount applies only to the specified product IDs.
        /// </para>
        /// </summary>
        public IReadOnlyList<string>? AppliesTo { get; set; }
    }

    public class BillingSubscription
    {
        public BillingSubscription(Subscription sub)
        {
            Status = sub?.Status;
            TrialStartDate = sub?.TrialStart;
            TrialEndDate = sub?.TrialEnd;
            var currentPeriod = sub?.GetCurrentPeriod();
            if (currentPeriod != null)
            {
                var (start, end) = currentPeriod.Value;
                PeriodStartDate = start;
                PeriodEndDate = end;
            }
            CancelledDate = sub?.CanceledAt;
            CancelAtEndDate = sub?.CancelAtPeriodEnd ?? false;
            var status = sub?.Status;
            Cancelled = status == "canceled" || status == "unpaid" || status == "incomplete_expired";
            if (sub?.Items?.Data != null)
            {
                Items = sub.Items.Data.Select(i => new BillingSubscriptionItem(i));
            }
            CollectionMethod = sub?.CollectionMethod;
            GracePeriod = sub?.CollectionMethod == "charge_automatically"
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
                    Amount = ConvertFromStripeMinorUnits(item.Plan.Amount) ?? 0;
                    Interval = item.Plan.Interval;

                    if (item.Metadata != null)
                    {
                        AddonSubscriptionItem = item.Metadata.TryGetValue("isAddOn", out var value) && bool.Parse(value);
                    }
                }

                Quantity = (int)item.Quantity;
                SponsoredSubscriptionItem = item.Plan != null && SponsoredPlans.All.Any(p => p.StripePlanId == item.Plan.Id);
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
            Amount = ConvertFromStripeMinorUnits(inv.AmountDue) ?? 0;
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
