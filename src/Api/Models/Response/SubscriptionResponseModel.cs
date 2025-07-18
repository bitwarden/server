﻿// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.Billing.Models.Business;
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
}

public class BillingCustomerDiscount(SubscriptionInfo.BillingCustomerDiscount discount)
{
    public string Id { get; } = discount.Id;
    public bool Active { get; } = discount.Active;
    public decimal? PercentOff { get; } = discount.PercentOff;
    public List<string> AppliesTo { get; } = discount.AppliesTo;
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
    public string Status { get; set; }
    public bool Cancelled { get; set; }
    public IEnumerable<BillingSubscriptionItem> Items { get; set; } = new List<BillingSubscriptionItem>();
    public string CollectionMethod { get; set; }
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

        public string ProductId { get; set; }
        public string Name { get; set; }
        public decimal Amount { get; set; }
        public int Quantity { get; set; }
        public string Interval { get; set; }
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
