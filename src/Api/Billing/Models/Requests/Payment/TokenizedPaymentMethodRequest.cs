#nullable enable
using System.ComponentModel.DataAnnotations;
using Bit.Api.Utilities;
using Bit.Core.Billing.Payment.Models;

namespace Bit.Api.Billing.Models.Requests.Payment;

public class TokenizedPaymentMethodRequest
{
    [Required]
    [StringMatches("bankAccount", "card", "payPal",
        ErrorMessage = "Payment method type must be one of: bankAccount, card, payPal")]
    public required string Type { get; set; }

    [Required]
    public required string Token { get; set; }

    public MinimalBillingAddressRequest? BillingAddress { get; set; }

    public (TokenizedPaymentMethod, BillingAddress?) ToDomain()
    {
        var paymentMethod = new TokenizedPaymentMethod
        {
            Type = Type switch
            {
                "bankAccount" => TokenizablePaymentMethodType.BankAccount,
                "card" => TokenizablePaymentMethodType.Card,
                "payPal" => TokenizablePaymentMethodType.PayPal,
                _ => throw new InvalidOperationException(
                    $"Invalid value for {nameof(TokenizedPaymentMethod)}.{nameof(TokenizedPaymentMethod.Type)}")
            },
            Token = Token
        };

        var billingAddress = BillingAddress?.ToDomain();

        return (paymentMethod, billingAddress);
    }
}
