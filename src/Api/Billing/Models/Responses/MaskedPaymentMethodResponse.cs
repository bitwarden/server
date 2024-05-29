using Bit.Core.Billing.Models;
using Bit.Core.Enums;

namespace Bit.Api.Billing.Models.Responses;

public record MaskedPaymentMethodResponse(
    PaymentMethodType Type,
    string Description,
    bool NeedsVerification)
{
    public static MaskedPaymentMethodResponse From(MaskedPaymentMethodDTO maskedPaymentMethod)
        => new(
            maskedPaymentMethod.Type,
            maskedPaymentMethod.Description,
            maskedPaymentMethod.NeedsVerification);
}
