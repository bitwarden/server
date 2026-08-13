using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Services.DiscountAudienceFilters;
using Bit.Core.Billing.Subscriptions.Entities;
using Bit.Core.Entities;
using Xunit;

namespace Bit.Core.Test.Billing.Services.DiscountAudienceFilters;

public class AllUsersFilterTests
{
    private readonly AllUsersFilter _sut = new();
    private readonly User _user = new();

    [Fact]
    public async Task IsUserEligible_NullStripeProductIds_ReturnsAllTiersTrue()
    {
        var discount = new SubscriptionDiscount { StripeProductIds = null };

        var result = await _sut.IsUserEligible(_user, discount);

        Assert.All(result.Values, Assert.True);
    }

    [Fact]
    public async Task IsUserEligible_EmptyStripeProductIds_ReturnsAllTiersTrue()
    {
        var discount = new SubscriptionDiscount { StripeProductIds = [] };

        var result = await _sut.IsUserEligible(_user, discount);

        Assert.All(result.Values, Assert.True);
    }

    [Fact]
    public async Task IsUserEligible_PremiumProductId_ReturnsPremiumTrueOthersFalse()
    {
        var discount = new SubscriptionDiscount { StripeProductIds = [StripeConstants.ProductIDs.Premium] };

        var result = await _sut.IsUserEligible(_user, discount);

        Assert.True(result[DiscountTierType.Premium]);
        Assert.False(result[DiscountTierType.Families]);
    }

    [Fact]
    public async Task IsUserEligible_FamiliesProductId_ReturnsFamiliesTrueOthersFalse()
    {
        var discount = new SubscriptionDiscount { StripeProductIds = [StripeConstants.ProductIDs.Families] };

        var result = await _sut.IsUserEligible(_user, discount);

        Assert.True(result[DiscountTierType.Families]);
        Assert.False(result[DiscountTierType.Premium]);
    }



    [Fact]
    public async Task IsUserEligible_UnknownProductId_ReturnsAllFalse()
    {
        var discount = new SubscriptionDiscount { StripeProductIds = ["prod_unknown"] };

        var result = await _sut.IsUserEligible(_user, discount);

        Assert.DoesNotContain(result.Values, v => v);
    }

    [Fact]
    public async Task IsUserEligible_MultipleKnownProductIds_ReturnsMappedTiersTrue()
    {
        var discount = new SubscriptionDiscount
        {
            StripeProductIds = [StripeConstants.ProductIDs.Premium, StripeConstants.ProductIDs.Families]
        };

        var result = await _sut.IsUserEligible(_user, discount);

        Assert.True(result[DiscountTierType.Premium]);
        Assert.True(result[DiscountTierType.Families]);
    }
}
