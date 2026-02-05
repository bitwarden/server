namespace Bit.Core.Billing.Pricing.Premium;

public class Plan
{
    public string Name { get; init; } = null!;
    public int? LegacyYear { get; init; }
    public bool Available { get; init; }
    public Purchasable Seat { get; init; } = null!;
    public Purchasable Storage { get; init; } = null!;
}
