namespace Bit.Core.Billing.Models;

public record PaymentInformation(
    long AccountCredit,
    MaskedPaymentMethod PaymentMethod,
    TaxInformation TaxInformation);
