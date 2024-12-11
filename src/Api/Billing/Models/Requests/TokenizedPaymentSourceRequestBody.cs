using System.ComponentModel.DataAnnotations;
using Bit.Api.Utilities;
using Bit.Core.Billing.Models;
using Bit.Core.Enums;

namespace Bit.Api.Billing.Models.Requests;

public class TokenizedPaymentSourceRequestBody
{
    [Required]
    [EnumMatches<PaymentMethodType>(
        PaymentMethodType.BankAccount,
        PaymentMethodType.Card,
        PaymentMethodType.PayPal,
        ErrorMessage = "'type' must be BankAccount, Card or PayPal"
    )]
    public PaymentMethodType Type { get; set; }

    [Required]
    public string Token { get; set; }

    public TokenizedPaymentSource ToDomain() => new(Type, Token);
}
