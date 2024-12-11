using Bit.Core.Enums;

namespace Bit.Core.Billing.Models;

public record TokenizedPaymentSource(PaymentMethodType Type, string Token);
