using System.ComponentModel.DataAnnotations;
using Bit.Api.Billing.Attributes;
using Bit.Core.Billing.Payment.Models;

namespace Bit.Api.Billing.Models.Requests.Payment;

public class MinimalTokenizedPaymentMethodRequest
{
    [Required]
    [PaymentMethodTypeValidation]
    public required string Type { get; set; }

    [Required]
    public required string Token { get; set; }

    public TokenizedPaymentMethod ToDomain() => new ()
    {
        Type = TokenizablePaymentMethodTypeExtensions.From(Type),
        Token = Token
    };
}
