using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Services;
using Bit.Core.Billing.Services.Implementations;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Repositories;
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
        Assert.False(metadata.HasSubscription);
        Assert.False(metadata.IsSubscriptionUnpaid);
        Assert.False(metadata.HasOpenInvoice);
        Assert.False(metadata.IsSubscriptionCanceled);
        Assert.Null(metadata.InvoiceDueDate);
        Assert.Null(metadata.InvoiceCreatedDate);
        Assert.Null(metadata.SubPeriodEndDate);
    }

    #endregion
}
