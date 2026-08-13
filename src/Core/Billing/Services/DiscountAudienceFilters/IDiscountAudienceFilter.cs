#nullable enable

using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Subscriptions.Entities;
using Bit.Core.Entities;

namespace Bit.Core.Billing.Services.DiscountAudienceFilters;

/// <summary>
/// Defines an eligibility check for a specific <see cref="Enums.DiscountAudienceType"/>.
/// Implementations are instantiated by <see cref="IDiscountAudienceFilterFactory"/> and
/// represent a single audience targeting rule.
/// </summary>
public interface IDiscountAudienceFilter
{
    /// <summary>
    /// The <see cref="DiscountAudienceType"/> this filter handles.
    /// </summary>
    DiscountAudienceType SupportedType { get; }

    /// <summary>
    /// Determines whether the given <paramref name="user"/> is eligible for the specified <paramref name="discount"/>
    /// </summary>
    /// <param name="user">The user to evaluate.</param>
    /// <param name="discount">The discount being evaluated for eligibility.</param>
    /// <returns>A per-tier eligibility matrix mapping each <see cref="DiscountTierType"/> to whether the user is eligible.</returns>
    Task<IDictionary<DiscountTierType, bool>> IsUserEligible(User user, SubscriptionDiscount discount);
}
