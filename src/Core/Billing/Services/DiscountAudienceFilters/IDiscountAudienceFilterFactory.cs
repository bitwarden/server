#nullable enable

using Bit.Core.Billing.Enums;

namespace Bit.Core.Billing.Services.DiscountAudienceFilters;

/// <summary>
/// Creates <see cref="IDiscountAudienceFilter"/> instances for a given <see cref="DiscountAudienceType"/>.
/// </summary>
public interface IDiscountAudienceFilterFactory
{
    /// <summary>
    /// Returns the <see cref="IDiscountAudienceFilter"/> for the specified <paramref name="audienceType"/>,
    /// or <see langword="null"/> if no filter is registered for that type.
    /// </summary>
    /// <param name="audienceType">The audience type to retrieve a filter for.</param>
    IDiscountAudienceFilter? GetFilter(DiscountAudienceType audienceType);
}
