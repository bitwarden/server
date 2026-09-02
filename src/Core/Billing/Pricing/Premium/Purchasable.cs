namespace Bit.Core.Billing.Pricing.Premium;

public class Purchasable
{
    public string StripePriceId { get; init; } = null!;
    public decimal Price { get; init; }
    public int Provided { get; init; }
}
