using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Services;
using Bit.Core.Billing.Services.DiscountAudienceFilters;
using Bit.Core.Billing.Subscriptions.Entities;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Repositories;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Stripe;
using Xunit;
using PremiumPlan = Bit.Core.Billing.Pricing.Premium.Plan;
using Purchasable = Bit.Core.Billing.Pricing.Premium.Purchasable;

namespace Bit.Core.Test.Billing.Services.DiscountAudienceFilters;

public class UserHasNoPreviousSubscriptionsFilterTests
{
    private readonly IPricingClient _pricingClient = Substitute.For<IPricingClient>();
    private readonly IStripeAdapter _stripeAdapter = Substitute.For<IStripeAdapter>();

    private readonly IOrganizationUserRepository _organizationUserRepository =
        Substitute.For<IOrganizationUserRepository>();

    private readonly UserHasNoPreviousSubscriptionsFilter _sut;

    public UserHasNoPreviousSubscriptionsFilterTests()
    {
        _sut = new UserHasNoPreviousSubscriptionsFilter(
            _stripeAdapter,
            _organizationUserRepository,
            _pricingClient);
    }

    [Theory, BitAutoData]
    public async Task IsUserEligible_PremiumProductId_UserHasPremium_ReturnsEligibleForPremiumFalse(
        User user,
        SubscriptionDiscount discount)
    {
        user.Premium = true;
        discount.StripeProductIds = [StripeConstants.ProductIDs.Premium];

        var result = await _sut.IsUserEligible(user, discount);

        Assert.False(result[DiscountTierType.Premium]);
        await _pricingClient.DidNotReceive().ListPremiumPlans();
        await _stripeAdapter.DidNotReceive().ListSubscriptionsAsync(Arg.Any<SubscriptionListOptions>());
    }


    [Theory, BitAutoData]
    public async Task IsUserEligible_PremiumProductId_HasGatewayCustomerId_NoPreviousPremiumSubscription_ReturnsEligibleForPremiumTrue(
        User user,
        SubscriptionDiscount discount)
    {
        user.Premium = false;
        user.GatewayCustomerId = "cus_123";
        discount.StripeProductIds = [StripeConstants.ProductIDs.Premium];
        const string premiumPriceId = "price_premium";

        _pricingClient
            .ListPremiumPlans()
            .Returns([new PremiumPlan { Seat = new Purchasable { StripePriceId = premiumPriceId } }]);

        _stripeAdapter
            .ListSubscriptionsAsync(Arg.Any<SubscriptionListOptions>())
            .Returns(new StripeList<Subscription> { Data = [] });


        var result = await _sut.IsUserEligible(user, discount);

        Assert.True(result[DiscountTierType.Premium]);
        await _pricingClient.Received(1).ListPremiumPlans();
        await _stripeAdapter.Received(1).ListSubscriptionsAsync(Arg.Any<SubscriptionListOptions>());
    }

    [Theory, BitAutoData]
    public async Task IsUserEligible_PremiumProductId_HasGatewayCustomerId_HasPreviousPremiumSubscription_ReturnsEligibleForPremiumFalse(
        User user,
        SubscriptionDiscount discount)
    {
        user.Premium = false;
        user.GatewayCustomerId = "cus_123";
        discount.StripeProductIds = [StripeConstants.ProductIDs.Premium];
        const string premiumPriceId = "price_premium";

        _pricingClient
            .ListPremiumPlans()
            .Returns([new PremiumPlan { Seat = new Purchasable { StripePriceId = premiumPriceId } }]);

        _stripeAdapter
            .ListSubscriptionsAsync(Arg.Any<SubscriptionListOptions>())
            .Returns(new StripeList<Subscription>
            {
                Data =
                [
                    new Subscription
                    {
                        Items = new StripeList<SubscriptionItem>
                        {
                            Data = [new SubscriptionItem { Price = new Price { Id = premiumPriceId } }]
                        }
                    }
                ]
            });

        _organizationUserRepository
            .GetManyDetailsByUserAsync(user.Id, OrganizationUserStatusType.Confirmed)
            .Returns(new List<OrganizationUserOrganizationDetails>
            {
                new() { Type = OrganizationUserType.Owner, PlanType = PlanType.FamiliesAnnually }
            });

        var result = await _sut.IsUserEligible(user, discount);

        Assert.False(result[DiscountTierType.Premium]);
        await _pricingClient.Received(1).ListPremiumPlans();
        await _stripeAdapter.Received(1).ListSubscriptionsAsync(Arg.Any<SubscriptionListOptions>());
    }

    [Theory, BitAutoData]
    public async Task IsUserEligible_PremiumProductId_NoPremium_NoGatewayCustomerId_ReturnsEligibleForPremiumTrue(
        User user,
        SubscriptionDiscount discount)
    {
        user.Premium = false;
        user.GatewayCustomerId = null;
        discount.StripeProductIds = [StripeConstants.ProductIDs.Premium];

        var result = await _sut.IsUserEligible(user, discount);

        Assert.True(result[DiscountTierType.Premium]);
        await _pricingClient.DidNotReceive().ListPremiumPlans();
        await _stripeAdapter.DidNotReceive().ListSubscriptionsAsync(Arg.Any<SubscriptionListOptions>());
    }

    [Theory, BitAutoData]
    public async Task IsUserEligible_FamilyProductId_UserDoesNotOwnFamiliesOrg_ReturnsFamiliesTrue(
        User user,
        SubscriptionDiscount discount)
    {
        discount.StripeProductIds = [StripeConstants.ProductIDs.Families];

        var result = await _sut.IsUserEligible(user, discount);

        Assert.True(result[DiscountTierType.Families]);
        await _organizationUserRepository.Received(1)
            .GetManyDetailsByUserAsync(user.Id, OrganizationUserStatusType.Confirmed);
    }

    [Theory, BitAutoData]
    public async Task IsUserEligible_FamilyProductId_UserOwnsFamiliesOrg_ReturnsFamiliesFalse(
        User user,
        SubscriptionDiscount discount)
    {
        discount.StripeProductIds = [StripeConstants.ProductIDs.Families];

        _organizationUserRepository
            .GetManyDetailsByUserAsync(user.Id, OrganizationUserStatusType.Confirmed)
            .Returns(new List<OrganizationUserOrganizationDetails>
            {
                new() { Type = OrganizationUserType.Owner, PlanType = PlanType.FamiliesAnnually }
            });

        var result = await _sut.IsUserEligible(user, discount);

        Assert.False(result[DiscountTierType.Families]);
        await _organizationUserRepository.Received(1)
            .GetManyDetailsByUserAsync(user.Id, OrganizationUserStatusType.Confirmed);
    }

    [Theory, BitAutoData]
    public async Task IsUserEligible_NoProductIds_UserOwnsFamiliesOrg_ReturnsPremiumTrueFamiliesFalse(
        User user,
        SubscriptionDiscount discount)
    {
        user.Premium = false;
        user.GatewayCustomerId = null;
        discount.StripeProductIds = null;

        _organizationUserRepository
            .GetManyDetailsByUserAsync(user.Id, OrganizationUserStatusType.Confirmed)
            .Returns(new List<OrganizationUserOrganizationDetails>
            {
                new() { Type = OrganizationUserType.Owner, PlanType = PlanType.FamiliesAnnually }
            });

        var result = await _sut.IsUserEligible(user, discount);

        Assert.True(result[DiscountTierType.Premium]);
        Assert.False(result[DiscountTierType.Families]);
        await _organizationUserRepository.Received(1)
            .GetManyDetailsByUserAsync(user.Id, OrganizationUserStatusType.Confirmed);
        await _pricingClient.DidNotReceive().ListPremiumPlans();
        await _stripeAdapter.DidNotReceive().ListSubscriptionsAsync(Arg.Any<SubscriptionListOptions>());
    }

    [Theory, BitAutoData]
    public async Task IsUserEligible_NoProductIds_UserDoesNotHavePremium_UserDoesNotOwnFamiliesOrg_ReturnsPremiumTrueFamiliesTrue(
        User user,
        SubscriptionDiscount discount)
    {
        user.Premium = false;
        user.GatewayCustomerId = null;
        discount.StripeProductIds = null;

        _organizationUserRepository
            .GetManyDetailsByUserAsync(user.Id, OrganizationUserStatusType.Confirmed)
            .Returns(new List<OrganizationUserOrganizationDetails>());

        var result = await _sut.IsUserEligible(user, discount);

        Assert.True(result[DiscountTierType.Premium]);
        Assert.True(result[DiscountTierType.Families]);
        await _organizationUserRepository.Received(1)
            .GetManyDetailsByUserAsync(user.Id, OrganizationUserStatusType.Confirmed);
        await _pricingClient.DidNotReceive().ListPremiumPlans();
        await _stripeAdapter.DidNotReceive().ListSubscriptionsAsync(Arg.Any<SubscriptionListOptions>());
    }

    [Theory, BitAutoData]
    public async Task
        IsUserEligible_NoProductIds_UserHasPremiumSubscription_DoesNotOwnFamiliesOrg_ReturnsPremiumFalseFamiliesTrue(
            User user,
            SubscriptionDiscount discount)
    {
        user.Premium = true;
        discount.StripeProductIds = null;

        _organizationUserRepository
            .GetManyDetailsByUserAsync(user.Id, OrganizationUserStatusType.Confirmed)
            .Returns(new List<OrganizationUserOrganizationDetails>());

        var result = await _sut.IsUserEligible(user, discount);

        Assert.False(result[DiscountTierType.Premium]);
        Assert.True(result[DiscountTierType.Families]);
        await _organizationUserRepository.Received(1)
            .GetManyDetailsByUserAsync(user.Id, OrganizationUserStatusType.Confirmed);
    }

    [Theory, BitAutoData]
    public async Task
        IsUserEligible_NoProductIds_UserHadPastPremiumSubscription_OwnsFamiliesOrg_ReturnsPremiumFalseFamiliesFalse(
            User user,
            SubscriptionDiscount discount)
    {
        user.Premium = false;
        user.GatewayCustomerId = "cus_123";
        discount.StripeProductIds = null;
        const string premiumPriceId = "price_premium";

        _pricingClient
            .ListPremiumPlans()
            .Returns([new PremiumPlan { Seat = new Purchasable { StripePriceId = premiumPriceId } }]);

        _stripeAdapter
            .ListSubscriptionsAsync(Arg.Any<SubscriptionListOptions>())
            .Returns(new StripeList<Subscription>
            {
                Data =
                [
                    new Subscription
                    {
                        Items = new StripeList<SubscriptionItem>
                        {
                            Data = [new SubscriptionItem { Price = new Price { Id = premiumPriceId } }]
                        }
                    }
                ]
            });

        _organizationUserRepository
            .GetManyDetailsByUserAsync(user.Id, OrganizationUserStatusType.Confirmed)
            .Returns(new List<OrganizationUserOrganizationDetails>
            {
                new() { Type = OrganizationUserType.Owner, PlanType = PlanType.FamiliesAnnually }
            });

        var result = await _sut.IsUserEligible(user, discount);

        Assert.False(result[DiscountTierType.Premium]);
        Assert.False(result[DiscountTierType.Families]);
        await _organizationUserRepository.Received(1)
            .GetManyDetailsByUserAsync(user.Id, OrganizationUserStatusType.Confirmed);
        await _pricingClient.Received(1).ListPremiumPlans();
        await _stripeAdapter.Received(1).ListSubscriptionsAsync(Arg.Any<SubscriptionListOptions>());
    }
}
