using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Services;
using Bit.Core.Billing.Services.Implementations;
using Bit.Core.Billing.Subscriptions.Entities;
using Bit.Core.Billing.Subscriptions.Repositories;
using Bit.Core.Entities;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using NSubstitute.ReturnsExtensions;
using Stripe;
using Xunit;

namespace Bit.Core.Test.Billing.Services;

[SutProviderCustomize]
public class SubscriptionDiscountServiceTests
{
    [Theory, BitAutoData]
    public async Task ValidateDiscountForUserAsync_DiscountNotFound_ReturnsFalse(
        User user,
        string stripeCouponId,
        SutProvider<SubscriptionDiscountService> sutProvider)
    {
        sutProvider.GetDependency<ISubscriptionDiscountRepository>()
            .GetByStripeCouponIdAsync(stripeCouponId)
            .ReturnsNull();

        var result = await sutProvider.Sut.ValidateDiscountForUserAsync(user, stripeCouponId, DiscountAudienceType.UserHasNoPreviousSubscriptions);

        Assert.False(result);
    }

    [Theory, BitAutoData]
    public async Task ValidateDiscountForUserAsync_DiscountNotYetActive_ReturnsFalse(
        User user,
        string stripeCouponId,
        SutProvider<SubscriptionDiscountService> sutProvider)
    {
        var discount = new SubscriptionDiscount
        {
            StripeCouponId = stripeCouponId,
            AudienceType = DiscountAudienceType.UserHasNoPreviousSubscriptions,
            StartDate = DateTime.UtcNow.AddDays(1),
            EndDate = DateTime.UtcNow.AddDays(30)
        };

        sutProvider.GetDependency<ISubscriptionDiscountRepository>()
            .GetByStripeCouponIdAsync(stripeCouponId)
            .Returns(discount);

        var result = await sutProvider.Sut.ValidateDiscountForUserAsync(user, stripeCouponId, DiscountAudienceType.UserHasNoPreviousSubscriptions);

        Assert.False(result);
    }

    [Theory, BitAutoData]
    public async Task ValidateDiscountForUserAsync_DiscountExpired_ReturnsFalse(
        User user,
        string stripeCouponId,
        SutProvider<SubscriptionDiscountService> sutProvider)
    {
        var discount = new SubscriptionDiscount
        {
            StripeCouponId = stripeCouponId,
            AudienceType = DiscountAudienceType.UserHasNoPreviousSubscriptions,
            StartDate = DateTime.UtcNow.AddDays(-30),
            EndDate = DateTime.UtcNow.AddDays(-1)
        };

        sutProvider.GetDependency<ISubscriptionDiscountRepository>()
            .GetByStripeCouponIdAsync(stripeCouponId)
            .Returns(discount);

        var result = await sutProvider.Sut.ValidateDiscountForUserAsync(user, stripeCouponId, DiscountAudienceType.UserHasNoPreviousSubscriptions);

        Assert.False(result);
    }

    [Theory, BitAutoData]
    public async Task ValidateDiscountForUserAsync_UserHasCurrentPremiumSubscription_ReturnsFalse(
        User user,
        string stripeCouponId,
        SutProvider<SubscriptionDiscountService> sutProvider)
    {
        user.Premium = true;
        user.GatewaySubscriptionId = "sub_123";

        var discount = new SubscriptionDiscount
        {
            StripeCouponId = stripeCouponId,
            AudienceType = DiscountAudienceType.UserHasNoPreviousSubscriptions,
            StartDate = DateTime.UtcNow.AddDays(-1),
            EndDate = DateTime.UtcNow.AddDays(30)
        };

        sutProvider.GetDependency<ISubscriptionDiscountRepository>()
            .GetByStripeCouponIdAsync(stripeCouponId)
            .Returns(discount);

        var result = await sutProvider.Sut.ValidateDiscountForUserAsync(user, stripeCouponId, DiscountAudienceType.UserHasNoPreviousSubscriptions);

        Assert.False(result);
    }

    [Theory, BitAutoData]
    public async Task ValidateDiscountForUserAsync_UserHasPastPremiumSubscription_ReturnsFalse(
        User user,
        string stripeCouponId,
        SutProvider<SubscriptionDiscountService> sutProvider)
    {
        user.Premium = false;
        user.GatewaySubscriptionId = null;
        user.GatewayCustomerId = "cus_123";

        var discount = new SubscriptionDiscount
        {
            StripeCouponId = stripeCouponId,
            AudienceType = DiscountAudienceType.UserHasNoPreviousSubscriptions,
            StartDate = DateTime.UtcNow.AddDays(-1),
            EndDate = DateTime.UtcNow.AddDays(30)
        };

        var canceledPremiumSubscription = new Subscription
        {
            Id = "sub_old",
            Status = "canceled",
            Items = new StripeList<SubscriptionItem>
            {
                Data = new List<SubscriptionItem>
                {
                    new()
                    {
                        Price = new Price { Id = "premium-annually" }
                    }
                }
            }
        };

        sutProvider.GetDependency<ISubscriptionDiscountRepository>()
            .GetByStripeCouponIdAsync(stripeCouponId)
            .Returns(discount);

        sutProvider.GetDependency<IStripeAdapter>()
            .ListSubscriptionsAsync(Arg.Is<SubscriptionListOptions>(opts =>
                opts.Customer == user.GatewayCustomerId))
            .Returns(new StripeList<Subscription>
            {
                Data = new List<Subscription> { canceledPremiumSubscription }
            });

        var result = await sutProvider.Sut.ValidateDiscountForUserAsync(user, stripeCouponId, DiscountAudienceType.UserHasNoPreviousSubscriptions);

        Assert.False(result);
    }

    [Theory, BitAutoData]
    public async Task ValidateDiscountForUserAsync_UserHasNoPremiumSubscriptions_ReturnsTrue(
        User user,
        string stripeCouponId,
        SutProvider<SubscriptionDiscountService> sutProvider)
    {
        user.Premium = false;
        user.GatewaySubscriptionId = null;
        user.GatewayCustomerId = "cus_123";

        var discount = new SubscriptionDiscount
        {
            StripeCouponId = stripeCouponId,
            AudienceType = DiscountAudienceType.UserHasNoPreviousSubscriptions,
            StartDate = DateTime.UtcNow.AddDays(-1),
            EndDate = DateTime.UtcNow.AddDays(30)
        };

        var nonPremiumSubscription = new Subscription
        {
            Id = "sub_org",
            Status = "active",
            Items = new StripeList<SubscriptionItem>
            {
                Data = new List<SubscriptionItem>
                {
                    new()
                    {
                        Price = new Price { Id = "teams-org-monthly" }
                    }
                }
            }
        };

        sutProvider.GetDependency<ISubscriptionDiscountRepository>()
            .GetByStripeCouponIdAsync(stripeCouponId)
            .Returns(discount);

        sutProvider.GetDependency<IStripeAdapter>()
            .ListSubscriptionsAsync(Arg.Is<SubscriptionListOptions>(opts =>
                opts.Customer == user.GatewayCustomerId))
            .Returns(new StripeList<Subscription>
            {
                Data = new List<Subscription> { nonPremiumSubscription }
            });

        var result = await sutProvider.Sut.ValidateDiscountForUserAsync(user, stripeCouponId, DiscountAudienceType.UserHasNoPreviousSubscriptions);

        Assert.True(result);
    }

    [Theory, BitAutoData]
    public async Task ValidateDiscountForUserAsync_UserHasNoStripeCustomer_ReturnsTrue(
        User user,
        string stripeCouponId,
        SutProvider<SubscriptionDiscountService> sutProvider)
    {
        user.Premium = false;
        user.GatewaySubscriptionId = null;
        user.GatewayCustomerId = null;

        var discount = new SubscriptionDiscount
        {
            StripeCouponId = stripeCouponId,
            AudienceType = DiscountAudienceType.UserHasNoPreviousSubscriptions,
            StartDate = DateTime.UtcNow.AddDays(-1),
            EndDate = DateTime.UtcNow.AddDays(30)
        };

        sutProvider.GetDependency<ISubscriptionDiscountRepository>()
            .GetByStripeCouponIdAsync(stripeCouponId)
            .Returns(discount);

        var result = await sutProvider.Sut.ValidateDiscountForUserAsync(user, stripeCouponId, DiscountAudienceType.UserHasNoPreviousSubscriptions);

        Assert.True(result);
    }

    [Theory, BitAutoData]
    public async Task ValidateDiscountForUserAsync_UserHasNoSubscriptions_ReturnsTrue(
        User user,
        string stripeCouponId,
        SutProvider<SubscriptionDiscountService> sutProvider)
    {
        user.Premium = false;
        user.GatewaySubscriptionId = null;
        user.GatewayCustomerId = "cus_123";

        var discount = new SubscriptionDiscount
        {
            StripeCouponId = stripeCouponId,
            AudienceType = DiscountAudienceType.UserHasNoPreviousSubscriptions,
            StartDate = DateTime.UtcNow.AddDays(-1),
            EndDate = DateTime.UtcNow.AddDays(30)
        };

        sutProvider.GetDependency<ISubscriptionDiscountRepository>()
            .GetByStripeCouponIdAsync(stripeCouponId)
            .Returns(discount);

        sutProvider.GetDependency<IStripeAdapter>()
            .ListSubscriptionsAsync(Arg.Is<SubscriptionListOptions>(opts =>
                opts.Customer == user.GatewayCustomerId))
            .Returns(new StripeList<Subscription>
            {
                Data = new List<Subscription>()
            });

        var result = await sutProvider.Sut.ValidateDiscountForUserAsync(user, stripeCouponId, DiscountAudienceType.UserHasNoPreviousSubscriptions);

        Assert.True(result);
    }

    [Theory, BitAutoData]
    public async Task ValidateDiscountForUserAsync_UserHasPremiumMonthlySubscription_ReturnsFalse(
        User user,
        string stripeCouponId,
        SutProvider<SubscriptionDiscountService> sutProvider)
    {
        user.Premium = false;
        user.GatewaySubscriptionId = null;
        user.GatewayCustomerId = "cus_123";

        var discount = new SubscriptionDiscount
        {
            StripeCouponId = stripeCouponId,
            AudienceType = DiscountAudienceType.UserHasNoPreviousSubscriptions,
            StartDate = DateTime.UtcNow.AddDays(-1),
            EndDate = DateTime.UtcNow.AddDays(30)
        };

        var premiumMonthlySubscription = new Subscription
        {
            Id = "sub_old",
            Status = "canceled",
            Items = new StripeList<SubscriptionItem>
            {
                Data = new List<SubscriptionItem>
                {
                    new()
                    {
                        Price = new Price { Id = "premium-monthly" }
                    }
                }
            }
        };

        sutProvider.GetDependency<ISubscriptionDiscountRepository>()
            .GetByStripeCouponIdAsync(stripeCouponId)
            .Returns(discount);

        sutProvider.GetDependency<IStripeAdapter>()
            .ListSubscriptionsAsync(Arg.Is<SubscriptionListOptions>(opts =>
                opts.Customer == user.GatewayCustomerId))
            .Returns(new StripeList<Subscription>
            {
                Data = new List<Subscription> { premiumMonthlySubscription }
            });

        var result = await sutProvider.Sut.ValidateDiscountForUserAsync(user, stripeCouponId, DiscountAudienceType.UserHasNoPreviousSubscriptions);

        Assert.False(result);
    }

    [Theory, BitAutoData]
    public async Task ValidateDiscountForUserAsync_AudienceTypeMismatch_ReturnsFalse(
        User user,
        string stripeCouponId,
        SutProvider<SubscriptionDiscountService> sutProvider)
    {
        user.Premium = false;
        user.GatewaySubscriptionId = null;
        user.GatewayCustomerId = null;

        var discount = new SubscriptionDiscount
        {
            StripeCouponId = stripeCouponId,
            AudienceType = DiscountAudienceType.UserHasNoPreviousSubscriptions,
            StartDate = DateTime.UtcNow.AddDays(-1),
            EndDate = DateTime.UtcNow.AddDays(30)
        };

        sutProvider.GetDependency<ISubscriptionDiscountRepository>()
            .GetByStripeCouponIdAsync(stripeCouponId)
            .Returns(discount);

        // Pass a different audience type than what the discount has
        // Note: This currently will fail because there's only one enum value
        // This test is future-proof for when more audience types are added
        const DiscountAudienceType differentAudienceType = (DiscountAudienceType)999;
        var result = await sutProvider.Sut.ValidateDiscountForUserAsync(user, stripeCouponId, differentAudienceType);

        Assert.False(result);
    }
}
