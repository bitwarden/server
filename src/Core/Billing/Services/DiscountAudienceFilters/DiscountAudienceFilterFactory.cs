#nullable enable

using Bit.Core.Billing.Enums;

namespace Bit.Core.Billing.Services.DiscountAudienceFilters;

/// <inheritdoc />
/// <remarks>
/// To add support for a new audience type: add an enum value, create a filter class,
/// implement <see cref="IDiscountAudienceFilter.SupportedType"/>, and register it in DI.
/// </remarks>
public class DiscountAudienceFilterFactory(
    IEnumerable<IDiscountAudienceFilter> filters) : IDiscountAudienceFilterFactory
{
    private readonly Dictionary<DiscountAudienceType, IDiscountAudienceFilter> _filters =
        filters.ToDictionary(f => f.SupportedType);

    public IDiscountAudienceFilter? GetFilter(DiscountAudienceType audienceType)
        => _filters.GetValueOrDefault(audienceType);
}
