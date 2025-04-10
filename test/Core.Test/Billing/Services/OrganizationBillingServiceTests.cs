using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Models.Sales;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Services;
using Bit.Core.Billing.Services.Implementations;
using Bit.Core.Exceptions;
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
    #region Finalize

    [Theory]
    [BitAutoData]
    public async Task Finalize_ThrowsBadRequestException_WhenAnActiveSubscriptionAlreadyExists(
        OrganizationSale sale,
        SutProvider<OrganizationBillingService> sutProvider)
    {
        var existingSubscription = new Subscription
        {
            Id = sale.Organization.GatewaySubscriptionId,
            Status = StripeConstants.SubscriptionStatus.Active
        };
        sutProvider.GetDependency<IStripeAdapter>()
            .SubscriptionGetAsync(
                Arg.Is<string>(p => p == sale.Organization.GatewaySubscriptionId),
                Arg.Any<SubscriptionGetOptions>())
            .Returns(existingSubscription);

        var actual = await Assert.ThrowsAsync<BadRequestException>(async () =>
            await sutProvider.Sut.Finalize(sale));

        Assert.Equal("You cannot create another subscription if you already have a active subscription.", actual.Message);
    }

    #endregion

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

        subscriberService
            .GetCustomer(organization, Arg.Is<CustomerGetOptions>(options => options.Expand.FirstOrDefault() == "discount.coupon.applies_to"))
            .Returns(new Customer
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
            });

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

        var metadata = await sutProvider.Sut.GetMetadata(organizationId);

        Assert.True(metadata!.IsOnSecretsManagerStandalone);
    }

    #endregion
}
