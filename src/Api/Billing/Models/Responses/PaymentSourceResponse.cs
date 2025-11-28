using Bit.Core.Billing.Models;
using Bit.Core.Enums;

namespace Bit.Api.Billing.Models.Responses;

public record PaymentSourceResponse(
    PaymentMethodType Type,
    string Description,
    bool NeedsVerification)
{
    public static PaymentSourceResponse From(PaymentSource paymentMethod)
        => new(
            paymentMethod.Type,
            paymentMethod.Description,
            paymentMethod.NeedsVerification);
}
