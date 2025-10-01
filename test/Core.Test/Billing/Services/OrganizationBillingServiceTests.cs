using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Caches;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Organizations.Models;
using Bit.Core.Billing.Organizations.Services;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Services;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Stripe;
using Xunit;

namespace Bit.Core.Test.Billing.Services;

[SutProviderCustomize]
public class OrganizationBillingServiceTests
{
    #region GetMetadata

    [Theory, BitAutoData]
    public async Task GetMetadata_Succeeds(
        Guid organizationId,
        Organization organization,
        SutProvider<OrganizationBillingService> sutProvider)
    {
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organizationId).Returns(organization);
        sutProvider.GetDependency<IPricingClient>().ListPlans().Returns(StaticStore.Plans.ToList());

        sutProvider.GetDependency<IPricingClient>().GetPlanOrThrow(organization.PlanType)
            .Returns(StaticStore.GetPlan(organization.PlanType));

        var subscriberService = sutProvider.GetDependency<ISubscriberService>();
        var organizationSeatCount = new OrganizationSeatCounts { Users = 1, Sponsored = 0 };
        var customer = new Customer
        {
            Discount = new Discount
            {
                Coupon = new Coupon
                {
                    Id = StripeConstants.CouponIDs.SecretsManagerStandalone,
                    AppliesTo = new CouponAppliesTo
                    {
                        Products = ["product_id"]
                    }
                }
            }
        };

        subscriberService
            .GetCustomer(organization, Arg.Is<CustomerGetOptions>(options =>
                options.Expand.Contains("discount.coupon.applies_to")))
            .Returns(customer);

        subscriberService.GetSubscription(organization).Returns(new Subscription
        {
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem
                    {
                        Plan = new Plan
                        {
                            ProductId = "product_id"
                        }
                    }
                ]
            }
        });

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetOccupiedSeatCountByOrganizationIdAsync(organization.Id)
            .Returns(new OrganizationSeatCounts { Users = 1, Sponsored = 0 });

        var metadata = await sutProvider.Sut.GetMetadata(organizationId);

        Assert.True(metadata!.IsOnSecretsManagerStandalone);
    }

    #endregion

    #region GetMetadata - Null Customer or Subscription

    [Theory, BitAutoData]
    public async Task GetMetadata_WhenCustomerOrSubscriptionIsNull_ReturnsDefaultMetadata(
        Guid organizationId,
        Organization organization,
        SutProvider<OrganizationBillingService> sutProvider)
    {
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organizationId).Returns(organization);

        sutProvider.GetDependency<IPricingClient>().ListPlans().Returns(StaticStore.Plans.ToList());

        sutProvider.GetDependency<IPricingClient>().GetPlanOrThrow(organization.PlanType)
            .Returns(StaticStore.GetPlan(organization.PlanType));

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetOccupiedSeatCountByOrganizationIdAsync(organization.Id)
            .Returns(new OrganizationSeatCounts { Users = 1, Sponsored = 0 });

        var subscriberService = sutProvider.GetDependency<ISubscriberService>();

        // Set up subscriber service to return null for customer
        subscriberService
            .GetCustomer(organization, Arg.Is<CustomerGetOptions>(options => options.Expand.FirstOrDefault() == "discount.coupon.applies_to"))
            .Returns((Customer)null);

        // Set up subscriber service to return null for subscription
        subscriberService.GetSubscription(organization).Returns((Subscription)null);

        var metadata = await sutProvider.Sut.GetMetadata(organizationId);

        Assert.NotNull(metadata);
        Assert.False(metadata!.IsOnSecretsManagerStandalone);
        Assert.Equal(1, metadata.OrganizationOccupiedSeats);
    }

    #endregion

    #region GetMetadata - Caching with Feature Flag

    [Theory, BitAutoData]
    public async Task GetMetadata_FeatureFlagOn_CacheHit_ReturnsFromCache(
        Guid organizationId,
        Organization organization,
        OrganizationMetadata cachedMetadata,
        SutProvider<OrganizationBillingService> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organizationId).Returns(organization);
        sutProvider.GetDependency<IFeatureService>().IsEnabled(FeatureFlagKeys.PM25379_UseNewOrganizationMetadataStructure)
            .Returns(true);
        sutProvider.GetDependency<IOrganizationMetadataCache>().Get(organizationId).Returns(cachedMetadata);

        // Act
        var result = await sutProvider.Sut.GetMetadata(organizationId);

        // Assert
        Assert.Equal(cachedMetadata, result);
        await sutProvider.GetDependency<ISubscriberService>().DidNotReceive()
            .GetCustomer(Arg.Any<Organization>(), Arg.Any<CustomerGetOptions>());
        await sutProvider.GetDependency<ISubscriberService>().DidNotReceive()
            .GetSubscription(Arg.Any<Organization>());
    }

    [Theory, BitAutoData]
    public async Task GetMetadata_FeatureFlagOn_CacheMiss_FetchesAndCaches(
        Guid organizationId,
        Organization organization,
        SutProvider<OrganizationBillingService> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organizationId).Returns(organization);
        sutProvider.GetDependency<IFeatureService>().IsEnabled(FeatureFlagKeys.PM25379_UseNewOrganizationMetadataStructure)
            .Returns(true);
        sutProvider.GetDependency<IOrganizationMetadataCache>().Get(organizationId).Returns((OrganizationMetadata)null);

        sutProvider.GetDependency<IPricingClient>().ListPlans().Returns(StaticStore.Plans.ToList());
        sutProvider.GetDependency<IPricingClient>().GetPlanOrThrow(organization.PlanType)
            .Returns(StaticStore.GetPlan(organization.PlanType));

        var customer = new Customer
        {
            Discount = new Discount
            {
                Coupon = new Coupon
                {
                    Id = StripeConstants.CouponIDs.SecretsManagerStandalone,
                    AppliesTo = new CouponAppliesTo
                    {
                        Products = ["product_id"]
                    }
                }
            }
        };

        sutProvider.GetDependency<ISubscriberService>()
            .GetCustomer(organization, Arg.Is<CustomerGetOptions>(options =>
                options.Expand.Contains("discount.coupon.applies_to")))
            .Returns(customer);

        sutProvider.GetDependency<ISubscriberService>().GetSubscription(organization).Returns(new Subscription
        {
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem
                    {
                        Plan = new Plan
                        {
                            ProductId = "product_id"
                        }
                    }
                ]
            }
        });

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetOccupiedSeatCountByOrganizationIdAsync(organization.Id)
            .Returns(new OrganizationSeatCounts { Users = 5, Sponsored = 0 });

        // Act
        var result = await sutProvider.Sut.GetMetadata(organizationId);

        // Assert
        Assert.NotNull(result);
        Assert.True(result!.IsOnSecretsManagerStandalone);
        Assert.Equal(5, result.OrganizationOccupiedSeats);

        await sutProvider.GetDependency<IOrganizationMetadataCache>().Received(1)
            .Set(organizationId, Arg.Is<OrganizationMetadata>(m =>
                m.IsOnSecretsManagerStandalone && m.OrganizationOccupiedSeats == 5));
    }

    [Theory, BitAutoData]
    public async Task GetMetadata_FeatureFlagOff_SkipsCache(
        Guid organizationId,
        Organization organization,
        SutProvider<OrganizationBillingService> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organizationId).Returns(organization);
        sutProvider.GetDependency<IFeatureService>().IsEnabled(FeatureFlagKeys.PM25379_UseNewOrganizationMetadataStructure)
            .Returns(false);

        sutProvider.GetDependency<IPricingClient>().ListPlans().Returns(StaticStore.Plans.ToList());
        sutProvider.GetDependency<IPricingClient>().GetPlanOrThrow(organization.PlanType)
            .Returns(StaticStore.GetPlan(organization.PlanType));

        var customer = new Customer
        {
            Discount = new Discount
            {
                Coupon = new Coupon
                {
                    Id = StripeConstants.CouponIDs.SecretsManagerStandalone,
                    AppliesTo = new CouponAppliesTo
                    {
                        Products = ["product_id"]
                    }
                }
            }
        };

        sutProvider.GetDependency<ISubscriberService>()
            .GetCustomer(organization, Arg.Is<CustomerGetOptions>(options =>
                options.Expand.Contains("discount.coupon.applies_to")))
            .Returns(customer);

        sutProvider.GetDependency<ISubscriberService>().GetSubscription(organization).Returns(new Subscription
        {
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem
                    {
                        Plan = new Plan
                        {
                            ProductId = "product_id"
                        }
                    }
                ]
            }
        });

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetOccupiedSeatCountByOrganizationIdAsync(organization.Id)
            .Returns(new OrganizationSeatCounts { Users = 3, Sponsored = 0 });

        // Act
        var result = await sutProvider.Sut.GetMetadata(organizationId);

        // Assert
        Assert.NotNull(result);
        Assert.True(result!.IsOnSecretsManagerStandalone);
        Assert.Equal(3, result.OrganizationOccupiedSeats);

        // Verify cache was never accessed
        await sutProvider.GetDependency<IOrganizationMetadataCache>().DidNotReceive()
            .Get(Arg.Any<Guid>());
        await sutProvider.GetDependency<IOrganizationMetadataCache>().DidNotReceive()
            .Set(Arg.Any<Guid>(), Arg.Any<OrganizationMetadata>());
    }

    #endregion
}
