using System.Security.Claims;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Licenses;
using Bit.Core.Billing.Licenses.Extensions;
using Bit.Core.Billing.Models.Business;
using Bit.Core.Entities;
using Bit.Core.Models.Api;
using Bit.Core.Models.Business;
using Bit.Core.Utilities;

namespace Bit.Api.Models.Response;

public class SubscriptionResponseModel : ResponseModel
{

    /// <param name="user">The user entity containing storage and premium subscription information</param>
    /// <param name="subscription">Subscription information retrieved from the payment provider (Stripe/Braintree)</param>
    /// <param name="license">The user's license containing expiration and feature entitlements</param>
    /// <param name="includeMilestone2Discount">
    /// Whether to include discount information in the response.
    /// Set to true when the PM23341_Milestone_2 feature flag is enabled AND
    /// you want to expose Milestone 2 discount information to the client.
    /// The discount will only be included if it matches the specific Milestone 2 coupon ID.
    /// </param>
    public SubscriptionResponseModel(User user, SubscriptionInfo subscription, UserLicense license, bool includeMilestone2Discount = false)
        : base("subscription")
    {
        Subscription = subscription.Subscription != null ? new BillingSubscription(subscription.Subscription) : null;
        UpcomingInvoice = subscription.UpcomingInvoice != null ?
            new BillingSubscriptionUpcomingInvoice(subscription.UpcomingInvoice) : null;
        StorageName = user.Storage.HasValue ? CoreHelpers.ReadableBytesSize(user.Storage.Value) : null;
        StorageGb = user.Storage.HasValue ? Math.Round(user.Storage.Value / 1073741824D, 2) : 0; // 1 GB
        MaxStorageGb = user.MaxStorageGb;
        License = license;
        Expiration = License.Expires;

        // Only display the Milestone 2 subscription discount on the subscription page.
        CustomerDiscount = ShouldIncludeMilestone2Discount(includeMilestone2Discount, subscription.CustomerDiscount)
            ? new BillingCustomerDiscount(subscription.CustomerDiscount!)
            : null;
    }

    /// <param name="user">The user entity containing storage and premium subscription information</param>
    /// <param name="subscription">Subscription information retrieved from the payment provider (Stripe/Braintree)</param>
    /// <param name="license">The user's license containing expiration and feature entitlements</param>
    /// <param name="claimsPrincipal">The claims principal containing cryptographically secure token claims</param>
    /// <param name="includeMilestone2Discount">
    /// Whether to include discount information in the response.
    /// Set to true when the PM23341_Milestone_2 feature flag is enabled AND
    /// you want to expose Milestone 2 discount information to the client.
    /// The discount will only be included if it matches the specific Milestone 2 coupon ID.
    /// </param>
    public SubscriptionResponseModel(User user, SubscriptionInfo? subscription, UserLicense license, ClaimsPrincipal? claimsPrincipal, bool includeMilestone2Discount = false)
        : base("subscription")
    {
        Subscription = subscription?.Subscription != null ? new BillingSubscription(subscription.Subscription) : null;
        UpcomingInvoice = subscription?.UpcomingInvoice != null ?
            new BillingSubscriptionUpcomingInvoice(subscription.UpcomingInvoice) : null;
        StorageName = user.Storage.HasValue ? CoreHelpers.ReadableBytesSize(user.Storage.Value) : null;
        StorageGb = user.Storage.HasValue ? Math.Round(user.Storage.Value / 1073741824D, 2) : 0; // 1 GB
        MaxStorageGb = user.MaxStorageGb;
        License = license;

        // CRITICAL: When a license has a Token (JWT), ALWAYS use the expiration from the token claim
        // The token's expiration is cryptographically secured and cannot be tampered with
        // The file's Expires property can be manually edited and should NOT be trusted for display
        if (claimsPrincipal != null)
        {
            Expiration = claimsPrincipal.GetValue<DateTime?>(UserLicenseConstants.Expires);
        }
        else
        {
            // No token - use the license file expiration (for older licenses without tokens)
            Expiration = License.Expires;
        }

        // Only display the Milestone 2 subscription discount on the subscription page.
        CustomerDiscount = ShouldIncludeMilestone2Discount(includeMilestone2Discount, subscription?.CustomerDiscount)
            ? new BillingCustomerDiscount(subscription!.CustomerDiscount!)
            : null;
    }

    public SubscriptionResponseModel(User user, UserLicense? license = null)
        : base("subscription")
    {
        StorageName = user.Storage.HasValue ? CoreHelpers.ReadableBytesSize(user.Storage.Value) : null;
        StorageGb = user.Storage.HasValue ? Math.Round(user.Storage.Value / 1073741824D, 2) : 0; // 1 GB
        MaxStorageGb = user.MaxStorageGb;
        Expiration = user.PremiumExpirationDate;

        if (license != null)
        {
            License = license;
        }
    }

    public string? StorageName { get; set; }
    public double? StorageGb { get; set; }
    public short? MaxStorageGb { get; set; }
    public BillingSubscriptionUpcomingInvoice? UpcomingInvoice { get; set; }
    public BillingSubscription? Subscription { get; set; }
    /// <summary>
    /// Customer discount information from Stripe for the Milestone 2 subscription discount.
    /// Only includes the specific Milestone 2 coupon (cm3nHfO1) when it's a perpetual discount (no expiration).
    /// This is for display purposes only and does not affect Stripe's automatic discount application.
    /// Other discounts may still apply in Stripe billing but are not included in this response.
    /// <para>
    /// Null when:
    /// - The PM23341_Milestone_2 feature flag is disabled
    /// - There is no active discount
    /// - The discount coupon ID doesn't match the Milestone 2 coupon (cm3nHfO1)
    /// - The instance is self-hosted
    /// </para>
    /// </summary>
    public BillingCustomerDiscount? CustomerDiscount { get; set; }
    public UserLicense? License { get; set; }
    public DateTime? Expiration { get; set; }

    /// <summary>
    /// Determines whether the Milestone 2 discount should be included in the response.
    /// </summary>
    /// <param name="includeMilestone2Discount">Whether the feature flag is enabled and discount should be considered.</param>
    /// <param name="customerDiscount">The customer discount from subscription info, if any.</param>
    /// <returns>True if the discount should be included; false otherwise.</returns>
    private static bool ShouldIncludeMilestone2Discount(
        bool includeMilestone2Discount,
        SubscriptionInfo.BillingCustomerDiscount? customerDiscount)
    {
        return includeMilestone2Discount &&
               customerDiscount != null &&
               customerDiscount.Id == StripeConstants.CouponIDs.Milestone2SubscriptionDiscount &&
               customerDiscount.Active;
    }
}

/// <summary>
/// Customer discount information from Stripe billing.
/// </summary>
public class BillingCustomerDiscount
{
    /// <summary>
    /// The Stripe coupon ID (e.g., "cm3nHfO1").
    /// </summary>
    public string? Id { get; }

    /// <summary>
    /// Whether the discount is a recurring/perpetual discount with no expiration date.
    /// <para>
    /// This property is true only when the discount has no end date, meaning it applies
    /// indefinitely to all future renewals. This is a product decision for Milestone 2
    /// to only display perpetual discounts in the UI.
    /// </para>
    /// <para>
    /// Note: This does NOT indicate whether the discount is "currently active" in the billing sense.
    /// A discount with a future end date is functionally active and will be applied by Stripe,
    /// but this property will be false because it has an expiration date.
    /// </para>
    /// </summary>
    public bool Active { get; }

    /// <summary>
    /// Percentage discount applied to the subscription (e.g., 20.0 for 20% off).
    /// Null if this is an amount-based discount.
    /// </summary>
    public decimal? PercentOff { get; }

    /// <summary>
    /// Fixed amount discount in USD (e.g., 14.00 for $14 off).
    /// Converted from Stripe's cent-based values (1400 cents → $14.00).
    /// Null if this is a percentage-based discount.
    /// Note: Stripe stores amounts in the smallest currency unit. This value is always in USD.
    /// </summary>
    public decimal? AmountOff { get; }

    /// <summary>
    /// List of Stripe product IDs that this discount applies to (e.g., ["prod_premium", "prod_families"]).
    /// <para>
    /// Null: discount applies to all products with no restrictions (AppliesTo not specified in Stripe).
    /// Empty list: discount restricted to zero products (edge case - AppliesTo.Products = [] in Stripe).
    /// Non-empty list: discount applies only to the specified product IDs.
    /// </para>
    /// </summary>
    public IReadOnlyList<string>? AppliesTo { get; }

    /// <summary>
    /// Creates a BillingCustomerDiscount from a SubscriptionInfo.BillingCustomerDiscount.
    /// </summary>
    /// <param name="discount">The discount to convert. Must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when discount is null.</exception>
    public BillingCustomerDiscount(SubscriptionInfo.BillingCustomerDiscount discount)
    {
        ArgumentNullException.ThrowIfNull(discount);

        Id = discount.Id;
        Active = discount.Active;
        PercentOff = discount.PercentOff;
        AmountOff = discount.AmountOff;
        AppliesTo = discount.AppliesTo;
    }
}

public class BillingSubscription
{
    public BillingSubscription(SubscriptionInfo.BillingSubscription sub)
    {
        Status = sub.Status;
        TrialStartDate = sub.TrialStartDate;
        TrialEndDate = sub.TrialEndDate;
        PeriodStartDate = sub.PeriodStartDate;
        PeriodEndDate = sub.PeriodEndDate;
        CancelledDate = sub.CancelledDate;
        CancelAtEndDate = sub.CancelAtEndDate;
        Cancelled = sub.Cancelled;
        if (sub.Items != null)
        {
            Items = sub.Items.Select(i => new BillingSubscriptionItem(i));
        }
        CollectionMethod = sub.CollectionMethod;
        SuspensionDate = sub.SuspensionDate;
        UnpaidPeriodEndDate = sub.UnpaidPeriodEndDate;
        GracePeriod = sub.GracePeriod;
    }

    public DateTime? TrialStartDate { get; set; }
    public DateTime? TrialEndDate { get; set; }
    public DateTime? PeriodStartDate { get; set; }
    public DateTime? PeriodEndDate { get; set; }
    public DateTime? CancelledDate { get; set; }
    public bool CancelAtEndDate { get; set; }
    public string? Status { get; set; }
    public bool Cancelled { get; set; }
    public IEnumerable<BillingSubscriptionItem> Items { get; set; } = new List<BillingSubscriptionItem>();
    public string? CollectionMethod { get; set; }
    public DateTime? SuspensionDate { get; set; }
    public DateTime? UnpaidPeriodEndDate { get; set; }
    public int? GracePeriod { get; set; }

    public class BillingSubscriptionItem
    {
        public BillingSubscriptionItem(SubscriptionInfo.BillingSubscription.BillingSubscriptionItem item)
        {
            ProductId = item.ProductId;
            Name = item.Name;
            Amount = item.Amount;
            Interval = item.Interval;
            Quantity = item.Quantity;
            SponsoredSubscriptionItem = item.SponsoredSubscriptionItem;
            AddonSubscriptionItem = item.AddonSubscriptionItem;
        }

        public string? ProductId { get; set; }
        public string? Name { get; set; }
        public decimal Amount { get; set; }
        public int Quantity { get; set; }
        public string? Interval { get; set; }
        public bool SponsoredSubscriptionItem { get; set; }
        public bool AddonSubscriptionItem { get; set; }
    }
}

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
