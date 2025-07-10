#nullable enable
namespace Bit.Core.Billing.Payment.Models;

public record TokenizedPaymentMethod
{
    public required TokenizablePaymentMethodType Type { get; set; }
    public required string Token { get; set; }
}
