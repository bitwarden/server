namespace Bit.Core.Models.Business;

public class InvoicePreviewResult
{
    public bool IsInvoicedNow { get; set; }
    public string PaymentIntentClientSecret { get; set; }
}
