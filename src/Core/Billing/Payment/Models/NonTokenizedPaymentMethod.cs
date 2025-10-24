namespace Bit.Core.Billing.Payment.Models;

public record NonTokenizedPaymentMethod
{
    public NonTokenizablePaymentMethodType Type { get; set; }
}

public enum NonTokenizablePaymentMethodType
{
    AccountCredit,
}
