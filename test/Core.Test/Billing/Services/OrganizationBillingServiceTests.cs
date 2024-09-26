using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Services;
using Bit.Core.Billing.Services.Implementations;
using Bit.Core.Repositories;
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
    public async Task GetMetadata_OrganizationNull_ReturnsNull(
        Guid organizationId,
        SutProvider<OrganizationBillingService> sutProvider)
    {
        var metadata = await sutProvider.Sut.GetMetadata(organizationId);

        Assert.Null(metadata);
    }

    [Theory, BitAutoData]
    public async Task GetMetadata_CustomerNull_ReturnsNull(
        Guid organizationId,
        Organization organization,
        SutProvider<OrganizationBillingService> sutProvider)
    {
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organizationId).Returns(organization);

        var metadata = await sutProvider.Sut.GetMetadata(organizationId);

        Assert.False(metadata.IsOnSecretsManagerStandalone);
    }

    [Theory, BitAutoData]
    public async Task GetMetadata_SubscriptionNull_ReturnsNull(
        Guid organizationId,
        Organization organization,
        SutProvider<OrganizationBillingService> sutProvider)
    {
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organizationId).Returns(organization);

        sutProvider.GetDependency<ISubscriberService>().GetCustomer(organization).Returns(new Customer());

        var metadata = await sutProvider.Sut.GetMetadata(organizationId);

        Assert.False(metadata.IsOnSecretsManagerStandalone);
    }

    [Theory, BitAutoData]
    public async Task GetMetadata_Succeeds(
        Guid organizationId,
        Organization organization,
        SutProvider<OrganizationBillingService> sutProvider)
    {
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organizationId).Returns(organization);

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

        Assert.True(metadata.IsOnSecretsManagerStandalone);
    }
    #endregion
}
