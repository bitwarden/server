#nullable enable
using System.ComponentModel.DataAnnotations;
using Bit.Api.Billing.Attributes;
using Bit.Core.Billing.Payment.Models;

namespace Bit.Api.Billing.Models.Requests.Payment;

public class TokenizedPaymentMethodRequest
{
    [Required]
    [PaymentMethodTypeValidation]
    public required string Type { get; set; }

    [Required]
    public required string Token { get; set; }

    public MinimalBillingAddressRequest? BillingAddress { get; set; }

    public (TokenizedPaymentMethod, BillingAddress?) ToDomain()
    {
        var paymentMethod = new TokenizedPaymentMethod
        {
            Type = TokenizablePaymentMethodTypeExtensions.From(Type),
            Token = Token
        };

        var billingAddress = BillingAddress?.ToDomain();

        return (paymentMethod, billingAddress);
    }
}
