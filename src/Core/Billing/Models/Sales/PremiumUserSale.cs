using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Business;

namespace Bit.Core.Billing.Models.Sales;

#nullable enable

public class PremiumUserSale
{
    private PremiumUserSale() { }

    public required User User { get; set; }
    public required CustomerSetup CustomerSetup { get; set; }
    public short? Storage { get; set; }

    public void Deconstruct(
        out User user,
        out CustomerSetup customerSetup,
        out short? storage)
    {
        user = User;
        customerSetup = CustomerSetup;
        storage = Storage;
    }

    public static PremiumUserSale From(
        User user,
        PaymentMethodType paymentMethodType,
        string paymentMethodToken,
        TaxInfo taxInfo,
        short? storage)
    {
        var tokenizedPaymentSource = new TokenizedPaymentSource(paymentMethodType, paymentMethodToken);

        var taxInformation = TaxInformation.From(taxInfo);

        return new PremiumUserSale
        {
            User = user,
            CustomerSetup = new CustomerSetup
            {
                TokenizedPaymentSource = tokenizedPaymentSource,
                TaxInformation = taxInformation
            },
            Storage = storage
        };
    }
}
