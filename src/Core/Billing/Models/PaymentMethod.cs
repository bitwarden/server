namespace Bit.Core.Billing.Models;

public record PaymentMethod(
    long AccountCredit,
    PaymentSource PaymentSource,
    TaxInformation TaxInformation);
