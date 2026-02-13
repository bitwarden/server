using Bit.Core.Billing.Payment.Models;

namespace Bit.Core.Billing.Premium.Models;

public record PremiumSubscriptionPurchase
{
    public required PaymentMethod PaymentMethod { get; init; }
    public required BillingAddress BillingAddress { get; init; }
    public short AdditionalStorageGb { get; init; }
    public string? Coupon { get; init; }
}
