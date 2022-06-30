using Bit.Core.Entities;
using Bit.Core.Models.Api;
using Bit.Core.Models.Business;
using Bit.Core.Utilities;

namespace Bit.Api.Models.Response;

public class SubscriptionResponseModel : ResponseModel
{
    public SubscriptionResponseModel(User user, SubscriptionInfo subscription, UserLicense license)
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
        UsingInAppPurchase = subscription.UsingInAppPurchase;
    }

    public SubscriptionResponseModel(User user, UserLicense license = null)
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

    public string StorageName { get; set; }
    public double? StorageGb { get; set; }
    public short? MaxStorageGb { get; set; }
    public BillingSubscriptionUpcomingInvoice UpcomingInvoice { get; set; }
    public BillingSubscription Subscription { get; set; }
    public UserLicense License { get; set; }
    public DateTime? Expiration { get; set; }
    public bool UsingInAppPurchase { get; set; }
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
    }

    public DateTime? TrialStartDate { get; set; }
    public DateTime? TrialEndDate { get; set; }
    public DateTime? PeriodStartDate { get; set; }
    public DateTime? PeriodEndDate { get; set; }
    public DateTime? CancelledDate { get; set; }
    public bool CancelAtEndDate { get; set; }
    public string Status { get; set; }
    public bool Cancelled { get; set; }
    public IEnumerable<BillingSubscriptionItem> Items { get; set; } = new List<BillingSubscriptionItem>();

    public class BillingSubscriptionItem
    {
        public BillingSubscriptionItem(SubscriptionInfo.BillingSubscription.BillingSubscriptionItem item)
        {
            Name = item.Name;
            Amount = item.Amount;
            Interval = item.Interval;
            Quantity = item.Quantity;
            SponsoredSubscriptionItem = item.SponsoredSubscriptionItem;
        }

        public string Name { get; set; }
        public decimal Amount { get; set; }
        public int Quantity { get; set; }
        public string Interval { get; set; }
        public bool SponsoredSubscriptionItem { get; set; }
    }
}

public class BillingSubscriptionUpcomingInvoice
{
    public BillingSubscriptionUpcomingInvoice(SubscriptionInfo.BillingUpcomingInvoice inv)
    {
        Amount = inv.Amount;
        Date = inv.Date;
    }

    public decimal Amount { get; set; }
    public DateTime? Date { get; set; }
}
