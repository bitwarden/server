using Bit.Core.Billing.Models;
using Bit.Core.Enums;

namespace Bit.Api.Billing.Models.Responses;

public record PaymentMethodResponse(
    PaymentMethodType Type,
    string Description,
    bool NeedsVerification)
{
    public static PaymentMethodResponse From(PaymentMethodDTO paymentMethod)
        => new (
            paymentMethod.Type,
            paymentMethod.Description,
            paymentMethod.NeedsVerification);
}
