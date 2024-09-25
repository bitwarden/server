using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Business;

namespace Bit.Core.Billing.Models.Sales;

#nullable enable

public class PremiumSale
{
    private PremiumSale() {}

    public void Deconstruct(
        out User user,
        out PaymentSetup paymentSetup,
        out int? storage)
    {
        user = User;
        paymentSetup = PaymentSetup;
        storage = Storage;
    }

    public static PremiumSale From(
        User user,
        PaymentMethodType paymentMethodType,
        string paymentMethodToken,
        TaxInfo taxInfo,
        int? storage)
    {
        var tokenizedPaymentSource = new TokenizedPaymentSource(paymentMethodType, paymentMethodToken);

        var taxInformation = new TaxInformation(
            taxInfo.BillingAddressCountry,
            taxInfo.BillingAddressPostalCode,
            taxInfo.TaxIdNumber,
            taxInfo.BillingAddressLine1,
            taxInfo.BillingAddressLine2,
            taxInfo.BillingAddressCity,
            taxInfo.BillingAddressState);

        return new PremiumSale
        {
            User = user,
            PaymentSetup = new PaymentSetup
            {
                TokenizedPaymentSource = tokenizedPaymentSource, TaxInformation = taxInformation
            },
            Storage = storage
        };
    }

    public required User User { get; set; }
    public required PaymentSetup PaymentSetup { get; set; }
    public int? Storage { get; set; }
}
