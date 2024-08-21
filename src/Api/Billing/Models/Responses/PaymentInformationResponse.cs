using Bit.Core.Billing.Models;

namespace Bit.Api.Billing.Models.Responses;

public record PaymentInformationResponse(
    long AccountCredit,
    MaskedPaymentMethod PaymentMethod,
    TaxInformation TaxInformation)
{
    public static PaymentInformationResponse From(PaymentInformation paymentInformation) =>
        new(
            paymentInformation.AccountCredit,
            paymentInformation.PaymentMethod,
            paymentInformation.TaxInformation);
}
