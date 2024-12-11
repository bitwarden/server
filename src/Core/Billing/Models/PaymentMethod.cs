namespace Bit.Core.Billing.Models;

public record PaymentMethod(
    long AccountCredit,
    PaymentSource PaymentSource,
    string SubscriptionStatus,
    TaxInformation TaxInformation
)
{
    public static PaymentMethod Empty => new(0, null, null, null);
}
