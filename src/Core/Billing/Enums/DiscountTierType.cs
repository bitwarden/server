namespace Bit.Core.Billing.Enums;

/// <summary>
/// Represents the product tiers that a subscription discount can target,
/// including both personal premium and organization-level tiers.
/// </summary>
public enum DiscountTierType : byte
{
    Premium = 0,
    Families = 1,
    Teams = 2,
    Enterprise = 3,
}
