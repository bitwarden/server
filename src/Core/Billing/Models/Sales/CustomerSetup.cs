namespace Bit.Core.Billing.Models.Sales;

#nullable enable

public class CustomerSetup
{
    public TokenizedPaymentSource? TokenizedPaymentSource { get; set; }
    public TaxInformation? TaxInformation { get; set; }
    public string? Coupon { get; set; }

    public bool IsBillable => TokenizedPaymentSource != null && TaxInformation != null;
}
