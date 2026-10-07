namespace Bit.Core.Billing.Enums;

/// <summary>
/// Defines the target audience for subscription discounts using an extensible strategy pattern.
/// Each audience type maps to specific eligibility rules implemented via IDiscountAudienceFilter.
/// </summary>
public enum DiscountAudienceType
{
    /// <summary>
    /// Discount applies to all users regardless of subscription history.
    /// This is the default value (0) when audience restrictions are not applied.
    /// </summary>
    AllUsers = 0,

    /// <summary>
    /// Discount applies to users who have never had a subscription before.
    /// </summary>
    UserHasNoPreviousSubscriptions = 1
}
