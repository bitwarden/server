using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Services;
using Bit.Core.Billing.Services.Implementations;
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

        sutProvider.GetDependency<IPricingClient>().GetPlanOrThrow(organization.PlanType)
            .Returns(StaticStore.GetPlan(organization.PlanType));

        var subscriberService = sutProvider.GetDependency<ISubscriberService>();

        subscriberService
            .GetCustomer(organization, Arg.Is<CustomerGetOptions>(options =>
                options.Expand.Contains("discount.coupon.applies_to") &&
                options.Expand.Contains("subscriptions") &&
                options.Expand.Contains("subscriptions.data.latest_invoice")))
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
                },
                InvoiceSettings = new CustomerInvoiceSettings
                {
                    DefaultPaymentMethodId = "pm_123"
                },
                Subscriptions = new StripeList<Subscription>
                {
                    Data = [
                        new Subscription
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
                        }
                    ]
                }
            });

        var metadata = await sutProvider.Sut.GetMetadata(organizationId);

        Assert.True(metadata!.IsOnSecretsManagerStandalone);
        Assert.True(metadata.IsPaymentMethodConfigured);
    }

    [Theory, BitAutoData]
    public async Task GetMetadata_ReturnsFalseForIsPaymentMethodConfigured_WhenNoPaymentMethodIsConfiguredForTheCustomer(
        Guid organizationId,
        Organization organization,
        SutProvider<OrganizationBillingService> sutProvider)
    {
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organizationId).Returns(organization);

        sutProvider.GetDependency<IPricingClient>().GetPlanOrThrow(organization.PlanType)
            .Returns(StaticStore.GetPlan(organization.PlanType));

        var subscriberService = sutProvider.GetDependency<ISubscriberService>();

        subscriberService
            .GetCustomer(organization, Arg.Is<CustomerGetOptions>(options =>
                options.Expand.Contains("discount.coupon.applies_to") &&
                options.Expand.Contains("subscriptions") &&
                options.Expand.Contains("subscriptions.data.latest_invoice")))
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
                },
                InvoiceSettings = new CustomerInvoiceSettings
                {
                    DefaultPaymentMethodId = null
                },
                Subscriptions = new StripeList<Subscription>
                {
                    Data = [
                        new Subscription
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
                        }
                    ]
                }
            });

        var metadata = await sutProvider.Sut.GetMetadata(organizationId);

        Assert.False(metadata!.IsPaymentMethodConfigured);
    }
    #endregion
}
