using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Services;
using Bit.Core.Billing.Services.DiscountAudienceFilters;
using Bit.Core.Repositories;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Billing.Services.DiscountAudienceFilters;

public class DiscountAudienceFilterFactoryTests
{
    private readonly DiscountAudienceFilterFactory _sut = new(
    [
        new AllUsersFilter(),
        new UserHasNoPreviousSubscriptionsFilter(
            Substitute.For<IStripeAdapter>(),
            Substitute.For<IOrganizationUserRepository>(),
            Substitute.For<IPricingClient>())
    ]);

    [Fact]
    public void GetFilter_UserHasNoPreviousSubscriptions_ReturnsCorrectFilter()
    {
        // Act
        var filter = _sut.GetFilter(DiscountAudienceType.UserHasNoPreviousSubscriptions);

        // Assert
        Assert.IsType<UserHasNoPreviousSubscriptionsFilter>(filter);
    }

    [Fact]
    public void GetFilter_AllUsers_ReturnsCorrectFilter()
    {
        // Act
        var filter = _sut.GetFilter(DiscountAudienceType.AllUsers);

        // Assert
        Assert.IsType<AllUsersFilter>(filter);
    }

    [Fact]
    public void GetFilter_UnknownAudienceType_ReturnsNull()
    {
        // Act
        var filter = _sut.GetFilter((DiscountAudienceType)99);

        // Assert
        Assert.Null(filter);
    }

    [Fact]
    public void GetFilter_AllDefinedAudienceTypes_ReturnsExpectedFilter()
    {
        // Arrange
        var allAudienceTypes = Enum.GetValues<DiscountAudienceType>();

        // Act & Assert: All defined audience types must return a non-null filter
        foreach (var audienceType in allAudienceTypes)
        {
            var filter = _sut.GetFilter(audienceType);
            Assert.NotNull(filter);
        }
    }
}
