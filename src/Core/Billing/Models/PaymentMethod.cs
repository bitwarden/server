// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.Billing.Tax.Models;

namespace Bit.Core.Billing.Models;

public record PaymentMethod(
    decimal AccountCredit,
    PaymentSource PaymentSource,
    string SubscriptionStatus,
    TaxInformation TaxInformation)
{
    public static PaymentMethod Empty => new(0, null, null, null);
}
