using Bit.Core.Models.Business;

namespace Bit.Api.Models.Response;

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
