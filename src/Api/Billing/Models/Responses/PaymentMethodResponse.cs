using Bit.Core.Billing.Models;

namespace Bit.Api.Billing.Models.Responses;

public record PaymentMethodResponse(
    long AccountCredit,
    PaymentSource PaymentSource,
    string SubscriptionStatus,
    TaxInformation TaxInformation
)
{
    public static PaymentMethodResponse From(PaymentMethod paymentMethod) =>
        new(
            paymentMethod.AccountCredit,
            paymentMethod.PaymentSource,
            paymentMethod.SubscriptionStatus,
            paymentMethod.TaxInformation
        );
}
