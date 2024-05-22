using Bit.Core.Enums;

namespace Bit.Core.Billing.Models;

public record TokenizedPaymentMethodDTO(
    PaymentMethodType Type,
    string Token);

