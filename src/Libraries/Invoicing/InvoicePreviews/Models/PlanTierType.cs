using System.Runtime.Serialization;

namespace Bit.Invoicing.InvoicePreviews.Models;

/// <summary>The four paid tiers with a cart to render. Strings match the client billing pricing-tier vocabulary. Free is excluded (no cart); TeamsStarter and custom collapse into Teams/Enterprise upstream.</summary>
public enum PlanTierType
{
    [EnumMember(Value = "families")] Families,
    [EnumMember(Value = "teams")] Teams,
    [EnumMember(Value = "enterprise")] Enterprise,
    [EnumMember(Value = "premium")] Premium
}
