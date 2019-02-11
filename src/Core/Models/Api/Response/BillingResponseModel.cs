using System;
using System.Linq;
using System.Collections.Generic;
using Bit.Core.Models.Business;
using Bit.Core.Models.Table;
using Bit.Core.Enums;

namespace Bit.Core.Models.Api
{
    public class BillingResponseModel : ResponseModel
    {
        public BillingResponseModel(User user, BillingInfo billing, UserLicense license)
            : base("billing")
        {
            CreditAmount = billing.CreditAmount;
            PaymentSource = billing.PaymentSource != null ? new BillingSource(billing.PaymentSource) : null;
            Subscription = billing.Subscription != null ? new BillingSubscription(billing.Subscription) : null;
            Transactions = billing.Transactions?.Select(t => new BillingTransaction(t));
            Invoices = billing.Invoices?.Select(i => new BillingInvoice(i));
            UpcomingInvoice = billing.UpcomingInvoice != null ? new BillingInvoiceInfo(billing.UpcomingInvoice) : null;
            StorageName = user.Storage.HasValue ? Utilities.CoreHelpers.ReadableBytesSize(user.Storage.Value) : null;
            StorageGb = user.Storage.HasValue ? Math.Round(user.Storage.Value / 1073741824D, 2) : 0; // 1 GB
            MaxStorageGb = user.MaxStorageGb;
            License = license;
            Expiration = License.Expires;
        }

        public BillingResponseModel(User user, UserLicense license = null)
            : base("billing")
        {
            StorageName = user.Storage.HasValue ? Utilities.CoreHelpers.ReadableBytesSize(user.Storage.Value) : null;
            StorageGb = user.Storage.HasValue ? Math.Round(user.Storage.Value / 1073741824D, 2) : 0; // 1 GB
            MaxStorageGb = user.MaxStorageGb;
            Expiration = user.PremiumExpirationDate;

            if(license != null)
            {
                License = license;
            }
        }

        public decimal CreditAmount { get; set; }
        public string StorageName { get; set; }
        public double? StorageGb { get; set; }
        public short? MaxStorageGb { get; set; }
        public BillingSource PaymentSource { get; set; }
        public BillingSubscription Subscription { get; set; }
        public BillingInvoiceInfo UpcomingInvoice { get; set; }
        public IEnumerable<BillingInvoice> Invoices { get; set; }
        public IEnumerable<BillingTransaction> Transactions { get; set; }
        public UserLicense License { get; set; }
        public DateTime? Expiration { get; set; }
    }

    public class BillingSource
    {
        public BillingSource(BillingInfo.BillingSource source)
        {
            Type = source.Type;
            CardBrand = source.CardBrand;
            Description = source.Description;
            NeedsVerification = source.NeedsVerification;
        }

        public PaymentMethodType Type { get; set; }
        public string CardBrand { get; set; }
        public string Description { get; set; }
        public bool NeedsVerification { get; set; }
    }

    public class BillingSubscription
    {
        public BillingSubscription(BillingInfo.BillingSubscription sub)
        {
            Status = sub.Status;
            TrialStartDate = sub.TrialStartDate;
            TrialEndDate = sub.TrialEndDate;
            PeriodStartDate = sub.PeriodStartDate;
            PeriodEndDate = sub.PeriodEndDate;
            CancelledDate = sub.CancelledDate;
            CancelAtEndDate = sub.CancelAtEndDate;
            Cancelled = sub.Cancelled;
            if(sub.Items != null)
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

    public class BillingInvoiceInfo
    {
        public BillingInvoiceInfo(BillingInfo.BillingInvoiceInfo inv)
        {
            Amount = inv.Amount;
            Date = inv.Date;
        }

        public decimal Amount { get; set; }
        public DateTime? Date { get; set; }
    }

    public class BillingInvoice : BillingInvoiceInfo
    {
        public BillingInvoice(BillingInfo.BillingInvoice inv)
            : base(inv)
        {
            Url = inv.Url;
            PdfUrl = inv.PdfUrl;
            Number = inv.Number;
            Paid = inv.Paid;
        }

        public string Url { get; set; }
        public string PdfUrl { get; set; }
        public string Number { get; set; }
        public bool Paid { get; set; }
    }

    public class BillingTransaction
    {
        public BillingTransaction(BillingInfo.BillingTransaction transaction)
        {
            CreatedDate = transaction.CreatedDate;
            Amount = transaction.Amount;
            Refunded = transaction.Refunded;
            RefundedAmount = transaction.RefundedAmount;
            PartiallyRefunded = transaction.PartiallyRefunded;
            Type = transaction.Type;
            PaymentMethodType = transaction.PaymentMethodType;
            Details = transaction.Details;
        }

        public DateTime CreatedDate { get; set; }
        public decimal Amount { get; set; }
        public bool? Refunded { get; set; }
        public bool? PartiallyRefunded { get; set; }
        public decimal? RefundedAmount { get; set; }
        public TransactionType Type { get; set; }
        public PaymentMethodType? PaymentMethodType { get; set; }
        public string Details { get; set; }
    }
}
