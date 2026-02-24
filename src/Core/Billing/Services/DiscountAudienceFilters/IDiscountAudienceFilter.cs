#nullable enable

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
    /// Determines whether the given <paramref name="user"/> meets the audience criteria
    /// required by the <paramref name="discount"/>.
    /// </summary>
    /// <param name="user">The user to evaluate.</param>
    /// <param name="discount">The discount whose audience criteria are being checked.</param>
    /// <returns><see langword="true"/> if the user is eligible; otherwise <see langword="false"/>.</returns>
    bool IsUserEligible(User user, SubscriptionDiscount discount);
}