using Bit.Core.Billing.Payment.Models;
using Bit.Core.Billing.Tax.Models;

namespace Bit.Core.Billing.Models.Sales;

#nullable enable

public class CustomerSetup
{
    public TokenizedPaymentSource? TokenizedPaymentSource { get; set; }
    public TaxInformation? TaxInformation { get; set; }
    public string? Coupon { get; set; }

    public bool IsBillable => TokenizedPaymentSource != null && TaxInformation != null;

    public static CustomerSetup From(TokenizedPaymentMethod paymentMethod, BillingAddress billingAddress)
    {
        return new CustomerSetup
        {
            TokenizedPaymentSource = TokenizedPaymentSource.From(paymentMethod),
            TaxInformation = new TaxInformation(
                billingAddress.Country,
                billingAddress.PostalCode,
                "",
                "",
                billingAddress.Line1 ?? "",
                billingAddress.Line2 ?? "",
                billingAddress.City ?? "",
                billingAddress.State ?? "")
        };
    }
}
