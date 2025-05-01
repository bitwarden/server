using Bit.Core.Enums;

namespace Bit.Core.Billing.Payment.Models;

public record TokenizedPaymentSource(
    PaymentMethodType Type,
    string Token);
