using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Services.Implementations;
using Bit.Core.Services;
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
    public async Task GetMetadata_OrganizationGatewayCustomerIdNull_ReturnsNull(
        Organization organization,
        SutProvider<OrganizationBillingService> sutProvider)
    {
        organization.GatewayCustomerId = null;

        var metadata = await sutProvider.Sut.GetMetadata(organization);

        Assert.Null(metadata);
    }

    [Theory, BitAutoData]
    public async Task GetMetadata_OrganizationGatewaySubscriptionIdNull_ReturnsNull(
        Organization organization,
        SutProvider<OrganizationBillingService> sutProvider)
    {
        organization.GatewaySubscriptionId = null;

        var metadata = await sutProvider.Sut.GetMetadata(organization);

        Assert.Null(metadata);
    }

    [Theory, BitAutoData]
    public async Task GetMetadata_CustomerNull_ReturnsDefault(
        Organization organization,
        SutProvider<OrganizationBillingService> sutProvider)
    {
        var metadata = await sutProvider.Sut.GetMetadata(organization);

        Assert.False(metadata!.IsOnSecretsManagerStandalone);
    }

    [Theory, BitAutoData]
    public async Task GetMetadata_SubscriptionNull_ReturnsDefault(
        Organization organization,
        SutProvider<OrganizationBillingService> sutProvider)
    {
        sutProvider.GetDependency<IStripeAdapter>().CustomerTryGetAsync(organization.GatewayCustomerId!).Returns(new Customer());

        var metadata = await sutProvider.Sut.GetMetadata(organization);

        Assert.False(metadata!.IsOnSecretsManagerStandalone);
    }

    [Theory, BitAutoData]
    public async Task GetMetadata_Succeeds(
        Organization organization,
        SutProvider<OrganizationBillingService> sutProvider)
    {
        var subscription = new Subscription
        {
            Id = organization.GatewaySubscriptionId,
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem { Plan = new Plan { ProductId = "product_id" } }
                ]
            }
        };

        var customer = new Customer
        {
            Discount = new Discount
            {
                Coupon = new Coupon
                {
                    Id = StripeConstants.CouponIDs.SecretsManagerStandalone,
                    AppliesTo = new CouponAppliesTo { Products = ["product_id"] }
                }
            },
            Subscriptions = new StripeList<Subscription> { Data = [subscription] }
        };

        sutProvider.GetDependency<IStripeAdapter>()
            .CustomerTryGetAsync(
                organization.GatewayCustomerId!,
                Arg.Is<CustomerGetOptions>(options => options.Expand.Contains("discount.coupon.applies_to") && options.Expand.Contains("subscriptions")))
            .Returns(customer);

        var metadata = await sutProvider.Sut.GetMetadata(organization);

        Assert.True(metadata!.IsOnSecretsManagerStandalone);
    }

    #endregion
}
