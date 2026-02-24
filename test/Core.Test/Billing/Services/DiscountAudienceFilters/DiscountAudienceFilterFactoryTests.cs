using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Services.DiscountAudienceFilters;
using Xunit;

namespace Bit.Core.Test.Billing.Services.DiscountAudienceFilters;

public class DiscountAudienceFilterFactoryTests
{
    private readonly DiscountAudienceFilterFactory _sut = new();

    [Fact]
    public void GetFilter_UserHasNoPreviousSubscriptions_ReturnsCorrectFilter()
    {
        // Act
        var filter = _sut.GetFilter(DiscountAudienceType.UserHasNoPreviousSubscriptions);

        // Assert
        Assert.IsType<UserHasNoPreviousSubscriptionsFilter>(filter);
    }

    [Fact]
    public void GetFilter_AllUsers_ReturnsNull()
    {
        // Act
        var filter = _sut.GetFilter(DiscountAudienceType.AllUsers);

        // Assert
        Assert.Null(filter);
    }

    [Fact]
    public void GetFilter_UnknownAudienceType_ReturnsNull()
    {
        // Act
        var filter = _sut.GetFilter((DiscountAudienceType)99);

        // Assert
        Assert.Null(filter);
    }
}
