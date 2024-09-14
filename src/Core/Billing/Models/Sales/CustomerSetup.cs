namespace Bit.Core.Billing.Models.Sales;

#nullable enable

public class CustomerSetup
{
    public required TokenizedPaymentSource TokenizedPaymentSource { get; set; }
    public required TaxInformation TaxInformation { get; set; }
    public string? Coupon { get; set; }
}
