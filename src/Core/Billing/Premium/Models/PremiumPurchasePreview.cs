namespace Bit.Core.Billing.Premium.Models;

public record PremiumPurchasePreview
{
    public short AdditionalStorageGb { get; init; }
    public string? Coupon { get; init; }
}
