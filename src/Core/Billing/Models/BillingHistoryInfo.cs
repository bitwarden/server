using Bit.Core.Entities;
using Bit.Core.Enums;
using Stripe;

namespace Bit.Core.Billing.Models;

public class BillingHistoryInfo
{
    public IEnumerable<BillingInvoice> Invoices { get; set; } = new List<BillingInvoice>();
    public IEnumerable<BillingTransaction> Transactions { get; set; } = new List<BillingTransaction>();

    public class BillingTransaction
    {
        public BillingTransaction(Transaction transaction)
        {
            Id = transaction.Id;
            CreatedDate = transaction.CreationDate;
            Refunded = transaction.Refunded;
            Type = transaction.Type;
            PaymentMethodType = transaction.PaymentMethodType;
            Details = transaction.Details;
            Amount = transaction.Amount;
            RefundedAmount = transaction.RefundedAmount;
        }

        public Guid Id { get; set; }
        public DateTime CreatedDate { get; set; }
        public decimal Amount { get; set; }
        public bool? Refunded { get; set; }
        public bool? PartiallyRefunded => !Refunded.GetValueOrDefault() && RefundedAmount.GetValueOrDefault() > 0;
        public decimal? RefundedAmount { get; set; }
        public TransactionType Type { get; set; }
        public PaymentMethodType? PaymentMethodType { get; set; }
        public string Details { get; set; }
    }

    public class BillingInvoice
    {
        public BillingInvoice(Invoice inv)
        {
            Id = inv.Id;
            Date = inv.Created;
            Url = inv.HostedInvoiceUrl;
            PdfUrl = inv.InvoicePdf;
            Number = inv.Number;
            Paid = inv.Paid;
            Amount = inv.Total / 100M;
        }

        public string Id { get; set; }
        public decimal Amount { get; set; }
        public DateTime? Date { get; set; }
        public string Url { get; set; }
        public string PdfUrl { get; set; }
        public string Number { get; set; }
        public bool Paid { get; set; }
    }

}
