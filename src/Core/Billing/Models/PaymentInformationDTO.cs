namespace Bit.Core.Billing.Models;

public record PaymentInformationDTO(
    long AccountCredit,
    MaskedPaymentMethodDTO PaymentMethod,
    TaxInformationDTO TaxInformation);
