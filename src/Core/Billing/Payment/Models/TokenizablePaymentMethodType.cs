namespace Bit.Core.Billing.Payment.Models;

public enum TokenizablePaymentMethodType
{
    BankAccount,
    Card,
    PayPal
}

public static class TokenizablePaymentMethodTypeExtensions
{
    public static TokenizablePaymentMethodType From(string type)
    {
        return type switch
        {
            "bankAccount" => TokenizablePaymentMethodType.BankAccount,
            "card" => TokenizablePaymentMethodType.Card,
            "payPal" => TokenizablePaymentMethodType.PayPal,
            _ => throw new InvalidOperationException($"Invalid value for {nameof(TokenizedPaymentMethod)}.{nameof(TokenizedPaymentMethod.Type)}")
        };
    }
}
