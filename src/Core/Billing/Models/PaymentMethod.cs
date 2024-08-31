namespace Bit.Core.Billing.Models;

public record PaymentMethod(
    long AccountCredit,
    PaymentSource PaymentSource,
    string SubscriptionStatus,
    TaxInformation TaxInformation);
