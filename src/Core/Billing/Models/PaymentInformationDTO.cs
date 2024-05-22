namespace Bit.Core.Billing.Models;

public record PaymentInformationDTO(
    long AccountCredit,
    PaymentMethodDTO PaymentMethod,
    TaxInformationDTO TaxInformation);
