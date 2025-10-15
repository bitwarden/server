﻿using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Organizations.Models;
using Bit.Core.Billing.Organizations.Queries;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Services;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using NSubstitute.ReturnsExtensions;
using Stripe;
using Xunit;

namespace Bit.Core.Test.Billing.Organizations.Queries;

[SutProviderCustomize]
public class GetOrganizationMetadataQueryTests
{
    [Theory, BitAutoData]
    public async Task Run_NullOrganization_ReturnsNull(
        SutProvider<GetOrganizationMetadataQuery> sutProvider)
    {
        var result = await sutProvider.Sut.Run(null);

        Assert.Null(result);
    }

    [Theory, BitAutoData]
    public async Task Run_SelfHosted_ReturnsDefault(
        Organization organization,
        SutProvider<GetOrganizationMetadataQuery> sutProvider)
    {
        sutProvider.GetDependency<IGlobalSettings>().SelfHosted.Returns(true);

        var result = await sutProvider.Sut.Run(organization);

        Assert.Equal(OrganizationMetadata.Default, result);
    }

    [Theory, BitAutoData]
    public async Task Run_NoGatewaySubscriptionId_ReturnsDefaultWithOccupiedSeats(
        Organization organization,
        SutProvider<GetOrganizationMetadataQuery> sutProvider)
    {
        organization.GatewaySubscriptionId = null;

        sutProvider.GetDependency<IGlobalSettings>().SelfHosted.Returns(false);
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetOccupiedSeatCountByOrganizationIdAsync(organization.Id)
            .Returns(new OrganizationSeatCounts { Users = 10, Sponsored = 0 });

        var result = await sutProvider.Sut.Run(organization);

        Assert.NotNull(result);
        Assert.False(result.IsOnSecretsManagerStandalone);
        Assert.Equal(10, result.OrganizationOccupiedSeats);
    }

    [Theory, BitAutoData]
    public async Task Run_NullCustomer_ReturnsDefaultWithOccupiedSeats(
        Organization organization,
        SutProvider<GetOrganizationMetadataQuery> sutProvider)
    {
        organization.GatewaySubscriptionId = "sub_123";

        sutProvider.GetDependency<IGlobalSettings>().SelfHosted.Returns(false);
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetOccupiedSeatCountByOrganizationIdAsync(organization.Id)
            .Returns(new OrganizationSeatCounts { Users = 5, Sponsored = 0 });

        sutProvider.GetDependency<ISubscriberService>()
            .GetCustomer(organization, Arg.Is<CustomerGetOptions>(options =>
                options.Expand.Contains("discount.coupon.applies_to")))
            .ReturnsNull();

        var result = await sutProvider.Sut.Run(organization);

        Assert.NotNull(result);
        Assert.False(result.IsOnSecretsManagerStandalone);
        Assert.Equal(5, result.OrganizationOccupiedSeats);
    }

    [Theory, BitAutoData]
    public async Task Run_NullSubscription_ReturnsDefaultWithOccupiedSeats(
        Organization organization,
        SutProvider<GetOrganizationMetadataQuery> sutProvider)
    {
        organization.GatewaySubscriptionId = "sub_123";

        var customer = new Customer();

        sutProvider.GetDependency<IGlobalSettings>().SelfHosted.Returns(false);
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetOccupiedSeatCountByOrganizationIdAsync(organization.Id)
            .Returns(new OrganizationSeatCounts { Users = 7, Sponsored = 0 });

        sutProvider.GetDependency<ISubscriberService>()
            .GetCustomer(organization, Arg.Is<CustomerGetOptions>(options =>
                options.Expand.Contains("discount.coupon.applies_to")))
            .Returns(customer);

        sutProvider.GetDependency<ISubscriberService>()
            .GetSubscription(organization)
            .ReturnsNull();

        var result = await sutProvider.Sut.Run(organization);

        Assert.NotNull(result);
        Assert.False(result.IsOnSecretsManagerStandalone);
        Assert.Equal(7, result.OrganizationOccupiedSeats);
    }

    [Theory, BitAutoData]
    public async Task Run_WithSecretsManagerStandaloneCoupon_ReturnsMetadataWithFlag(
        Organization organization,
        SutProvider<GetOrganizationMetadataQuery> sutProvider)
    {
        organization.GatewaySubscriptionId = "sub_123";
        organization.PlanType = PlanType.EnterpriseAnnually;

        var productId = "product_123";
        var customer = new Customer
        {
            Discount = new Discount
            {
                Coupon = new Coupon
                {
                    Id = StripeConstants.CouponIDs.SecretsManagerStandalone,
                    AppliesTo = new CouponAppliesTo
                    {
                        Products = [productId]
                    }
                }
            }
        };

        var subscription = new Subscription
        {
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem
                    {
                        Plan = new Plan
                        {
                            ProductId = productId
                        }
                    }
                ]
            }
        };

        sutProvider.GetDependency<IGlobalSettings>().SelfHosted.Returns(false);
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetOccupiedSeatCountByOrganizationIdAsync(organization.Id)
            .Returns(new OrganizationSeatCounts { Users = 15, Sponsored = 0 });

        sutProvider.GetDependency<ISubscriberService>()
            .GetCustomer(organization, Arg.Is<CustomerGetOptions>(options =>
                options.Expand.Contains("discount.coupon.applies_to")))
            .Returns(customer);

        sutProvider.GetDependency<ISubscriberService>()
            .GetSubscription(organization)
            .Returns(subscription);

        sutProvider.GetDependency<IPricingClient>()
            .GetPlanOrThrow(organization.PlanType)
            .Returns(StaticStore.GetPlan(organization.PlanType));

        var result = await sutProvider.Sut.Run(organization);

        Assert.NotNull(result);
        Assert.True(result.IsOnSecretsManagerStandalone);
        Assert.Equal(15, result.OrganizationOccupiedSeats);
    }

    [Theory, BitAutoData]
    public async Task Run_WithoutSecretsManagerStandaloneCoupon_ReturnsMetadataWithoutFlag(
        Organization organization,
        SutProvider<GetOrganizationMetadataQuery> sutProvider)
    {
        organization.GatewaySubscriptionId = "sub_123";
        organization.PlanType = PlanType.TeamsAnnually;

        var customer = new Customer
        {
            Discount = null
        };

        var subscription = new Subscription
        {
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem
                    {
                        Plan = new Plan
                        {
                            ProductId = "product_123"
                        }
                    }
                ]
            }
        };

        sutProvider.GetDependency<IGlobalSettings>().SelfHosted.Returns(false);
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetOccupiedSeatCountByOrganizationIdAsync(organization.Id)
            .Returns(new OrganizationSeatCounts { Users = 20, Sponsored = 0 });

        sutProvider.GetDependency<ISubscriberService>()
            .GetCustomer(organization, Arg.Is<CustomerGetOptions>(options =>
                options.Expand.Contains("discount.coupon.applies_to")))
            .Returns(customer);

        sutProvider.GetDependency<ISubscriberService>()
            .GetSubscription(organization)
            .Returns(subscription);

        sutProvider.GetDependency<IPricingClient>()
            .GetPlanOrThrow(organization.PlanType)
            .Returns(StaticStore.GetPlan(organization.PlanType));

        var result = await sutProvider.Sut.Run(organization);

        Assert.NotNull(result);
        Assert.False(result.IsOnSecretsManagerStandalone);
        Assert.Equal(20, result.OrganizationOccupiedSeats);
    }

    [Theory, BitAutoData]
    public async Task Run_CouponDoesNotApplyToSubscriptionProducts_ReturnsFalseForStandaloneFlag(
        Organization organization,
        SutProvider<GetOrganizationMetadataQuery> sutProvider)
    {
        organization.GatewaySubscriptionId = "sub_123";
        organization.PlanType = PlanType.EnterpriseAnnually;

        var customer = new Customer
        {
            Discount = new Discount
            {
                Coupon = new Coupon
                {
                    Id = StripeConstants.CouponIDs.SecretsManagerStandalone,
                    AppliesTo = new CouponAppliesTo
                    {
                        Products = ["different_product_id"]
                    }
                }
            }
        };

        var subscription = new Subscription
        {
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem
                    {
                        Plan = new Plan
                        {
                            ProductId = "product_123"
                        }
                    }
                ]
            }
        };

        sutProvider.GetDependency<IGlobalSettings>().SelfHosted.Returns(false);
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetOccupiedSeatCountByOrganizationIdAsync(organization.Id)
            .Returns(new OrganizationSeatCounts { Users = 12, Sponsored = 0 });

        sutProvider.GetDependency<ISubscriberService>()
            .GetCustomer(organization, Arg.Is<CustomerGetOptions>(options =>
                options.Expand.Contains("discount.coupon.applies_to")))
            .Returns(customer);

        sutProvider.GetDependency<ISubscriberService>()
            .GetSubscription(organization)
            .Returns(subscription);

        sutProvider.GetDependency<IPricingClient>()
            .GetPlanOrThrow(organization.PlanType)
            .Returns(StaticStore.GetPlan(organization.PlanType));

        var result = await sutProvider.Sut.Run(organization);

        Assert.NotNull(result);
        Assert.False(result.IsOnSecretsManagerStandalone);
        Assert.Equal(12, result.OrganizationOccupiedSeats);
    }

    [Theory, BitAutoData]
    public async Task Run_PlanDoesNotSupportSecretsManager_ReturnsFalseForStandaloneFlag(
        Organization organization,
        SutProvider<GetOrganizationMetadataQuery> sutProvider)
    {
        organization.GatewaySubscriptionId = "sub_123";
        organization.PlanType = PlanType.FamiliesAnnually;

        var productId = "product_123";
        var customer = new Customer
        {
            Discount = new Discount
            {
                Coupon = new Coupon
                {
                    Id = StripeConstants.CouponIDs.SecretsManagerStandalone,
                    AppliesTo = new CouponAppliesTo
                    {
                        Products = [productId]
                    }
                }
            }
        };

        var subscription = new Subscription
        {
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem
                    {
                        Plan = new Plan
                        {
                            ProductId = productId
                        }
                    }
                ]
            }
        };

        sutProvider.GetDependency<IGlobalSettings>().SelfHosted.Returns(false);
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetOccupiedSeatCountByOrganizationIdAsync(organization.Id)
            .Returns(new OrganizationSeatCounts { Users = 8, Sponsored = 0 });

        sutProvider.GetDependency<ISubscriberService>()
            .GetCustomer(organization, Arg.Is<CustomerGetOptions>(options =>
                options.Expand.Contains("discount.coupon.applies_to")))
            .Returns(customer);

        sutProvider.GetDependency<ISubscriberService>()
            .GetSubscription(organization)
            .Returns(subscription);

        sutProvider.GetDependency<IPricingClient>()
            .GetPlanOrThrow(organization.PlanType)
            .Returns(StaticStore.GetPlan(organization.PlanType));

        var result = await sutProvider.Sut.Run(organization);

        Assert.NotNull(result);
        Assert.False(result.IsOnSecretsManagerStandalone);
        Assert.Equal(8, result.OrganizationOccupiedSeats);
    }
}
