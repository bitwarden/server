using System;
using System.Linq;
using System.Collections.Generic;
using Bit.Core.Models.Business;
using Stripe;
using Bit.Core.Models.Table;
using Bit.Core.Enums;

namespace Bit.Core.Models.Api
{
    public class BillingResponseModel : ResponseModel
    {
        public BillingResponseModel(IStorable storable, BillingInfo billing)
            : base("billing")
        {
            PaymentSource = billing.PaymentSource != null ? new BillingSource(billing.PaymentSource) : null;
            Subscription = billing.Subscription != null ? new BillingSubscription(billing.Subscription) : null;
            Charges = billing.Charges.Select(c => new BillingCharge(c));
            UpcomingInvoice = billing.UpcomingInvoice != null ? new BillingInvoice(billing.UpcomingInvoice) : null;
            StorageName = storable.Storage.HasValue ? Utilities.CoreHelpers.ReadableBytesSize(storable.Storage.Value) : null;
            StorageGb = storable.Storage.HasValue ? Math.Round(storable.Storage.Value / 1073741824D, 2) : 0; // 1 GB
            MaxStorageGb = storable.MaxStorageGb;
        }

        public string StorageName { get; set; }
        public double? StorageGb { get; set; }
        public short? MaxStorageGb { get; set; }
        public BillingSource PaymentSource { get; set; }
        public BillingSubscription Subscription { get; set; }
        public BillingInvoice UpcomingInvoice { get; set; }
        public IEnumerable<BillingCharge> Charges { get; set; }
    }

    public class BillingSource
    {
        public BillingSource(BillingInfo.BillingSource source)
        {
            Type = source.Type;
            CardBrand = source.CardBrand;
            Description = source.Description;
        }

        public PaymentMethodType Type { get; set; }
        public string CardBrand { get; set; }
        public string Description { get; set; }
    }

    public class BillingSubscription
    {
        public BillingSubscription(BillingInfo.BillingSubscription sub)
        {
            Status = sub.Status;
            TrialStartDate = sub.TrialStartDate;
            TrialEndDate = sub.TrialEndDate;
            EndDate = sub.EndDate;
            CancelledDate = sub.CancelledDate;
            CancelAtEndDate = sub.CancelAtEndDate;
            Cancelled = Cancelled;
            if(sub.Items != null)
            {
                Items = sub.Items.Select(i => new BillingSubscriptionItem(i));
            }
        }

        public DateTime? TrialStartDate { get; set; }
        public DateTime? TrialEndDate { get; set; }
        public DateTime? EndDate { get; set; }
        public DateTime? CancelledDate { get; set; }
        public bool CancelAtEndDate { get; set; }
        public string Status { get; set; }
        public bool Cancelled { get; set; }
        public IEnumerable<BillingSubscriptionItem> Items { get; set; } = new List<BillingSubscriptionItem>();

        public class BillingSubscriptionItem
        {
            public BillingSubscriptionItem(BillingInfo.BillingSubscription.BillingSubscriptionItem item)
            {
                Name = item.Name;
                Amount = item.Amount;
                Interval = item.Interval;
                Quantity = item.Quantity;
            }

            public string Name { get; set; }
            public decimal Amount { get; set; }
            public int Quantity { get; set; }
            public string Interval { get; set; }
        }
    }

    public class BillingInvoice
    {
        public BillingInvoice(BillingInfo.BillingInvoice inv)
        {
            Amount = inv.Amount;
            Date = inv.Date;
        }

        public decimal Amount { get; set; }
        public DateTime? Date { get; set; }
    }

    public class BillingCharge
    {
        public BillingCharge(BillingInfo.BillingCharge charge)
        {
            Amount = charge.Amount;
            RefundedAmount = charge.RefundedAmount;
            PaymentSource = charge.PaymentSource != null ? new BillingSource(charge.PaymentSource) : null;
            CreatedDate = charge.CreatedDate;
            FailureMessage = charge.FailureMessage;
            Refunded = charge.Refunded;
            Status = charge.Status;
            InvoiceId = charge.InvoiceId;
        }

        public DateTime CreatedDate { get; set; }
        public decimal Amount { get; set; }
        public BillingSource PaymentSource { get; set; }
        public string Status { get; set; }
        public string FailureMessage { get; set; }
        public bool Refunded { get; set; }
        public bool PartiallyRefunded => !Refunded && RefundedAmount > 0;
        public decimal RefundedAmount { get; set; }
        public string InvoiceId { get; set; }
    }
}
