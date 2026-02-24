#nullable enable

using Bit.Core.Billing.Enums;

namespace Bit.Core.Billing.Services.DiscountAudienceFilters;

/// <inheritdoc />
/// <remarks>
/// To add support for a new audience type: add an enum value, create a filter class, and add a case here.
/// </remarks>
public class DiscountAudienceFilterFactory : IDiscountAudienceFilterFactory
{
    public IDiscountAudienceFilter? GetFilter(DiscountAudienceType audienceType) =>
        audienceType switch
        {
            DiscountAudienceType.UserHasNoPreviousSubscriptions => new UserHasNoPreviousSubscriptionsFilter(),
            _ => null
        };
}