using Bit.Core.Billing.Payment.Models;
using Bit.Core.Enums;

namespace Bit.Core.Billing.Models;

public record TokenizedPaymentSource(
    PaymentMethodType Type,
    string Token)
{
    public static TokenizedPaymentSource From(TokenizedPaymentMethod paymentMethod)
    {
        return new TokenizedPaymentSource(
            paymentMethod.Type switch
            {
                TokenizablePaymentMethodType.BankAccount => PaymentMethodType.BankAccount,
                TokenizablePaymentMethodType.Card => PaymentMethodType.Card,
                TokenizablePaymentMethodType.PayPal => PaymentMethodType.PayPal
            },
            paymentMethod.Token);
    }
}
