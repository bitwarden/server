using Bit.Core.Enums;

namespace Bit.Core.Billing.Models;

public record TokenizedPaymentMethod(
    PaymentMethodType Type,
    string Token);
