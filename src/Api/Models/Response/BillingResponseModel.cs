using Bit.Core.Enums;
using Bit.Core.Models.Api;
using Bit.Core.Models.Business;

namespace Bit.Api.Models.Response;

public class BillingResponseModel : ResponseModel
{
    public BillingResponseModel(BillingInfo billing)
        : base("billing")
    {
        Balance = billing.Balance;
        PaymentSource = billing.PaymentSource != null ? new BillingSource(billing.PaymentSource) : null;
        Transactions = billing.Transactions?.Select(t => new BillingTransaction(t));
        Invoices = billing.Invoices?.Select(i => new BillingInvoice(i));
    }

    public decimal Balance { get; set; }
    public BillingSource PaymentSource { get; set; }
    public IEnumerable<BillingInvoice> Invoices { get; set; }
    public IEnumerable<BillingTransaction> Transactions { get; set; }
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

public class BillingInvoice
{
    public BillingInvoice(BillingInfo.BillingInvoice inv)
    {
        Amount = inv.Amount;
        Date = inv.Date;
        Url = inv.Url;
        PdfUrl = inv.PdfUrl;
        Number = inv.Number;
        Paid = inv.Paid;
    }

    public decimal Amount { get; set; }
    public DateTime? Date { get; set; }
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
