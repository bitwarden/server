using Bit.Core.Billing.Models;

namespace Bit.Api.Billing.Models.Responses;

public record PaymentInformationResponse(
    long AccountCredit,
    MaskedPaymentMethodDTO PaymentMethod,
    TaxInformation TaxInformation)
{
    public static PaymentInformationResponse From(PaymentInformationDTO paymentInformation) =>
        new(
            paymentInformation.AccountCredit,
            paymentInformation.PaymentMethod,
            paymentInformation.TaxInformation);
}
