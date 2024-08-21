using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Models;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Stripe;

namespace Bit.Core.Billing.Services.Implementations;

public class OrganizationBillingService(
    IOrganizationRepository organizationRepository,
    IStripeAdapter stripeAdapter) : IOrganizationBillingService
{
    public async Task<OrganizationMetadata> GetMetadata(Guid organizationId)
    {
        var organization = await organizationRepository.GetByIdAsync(organizationId);

        if (organization == null || string.IsNullOrEmpty(organization.GatewayCustomerId))
        {
            return null;
        }

        var customer = await stripeAdapter.CustomerTryGet(organization.GatewayCustomerId, new CustomerGetOptions
        {
            Expand = ["discount.coupon.applies_to", "subscriptions"]
        });

        var subscription = customer?.Subscriptions.FirstOrDefault(subscription => subscription.Id == organization.GatewaySubscriptionId);

        if (customer == null || subscription == null)
        {
            return OrganizationMetadata.Default();
        }

        var isOnSecretsManagerStandalone = IsOnSecretsManagerStandalone(organization, customer, subscription);

        return new OrganizationMetadata(isOnSecretsManagerStandalone);
    }

    private static bool IsOnSecretsManagerStandalone(
        Organization organization,
        Customer customer,
        Subscription subscription)
    {
        var plan = StaticStore.GetPlan(organization.PlanType);

        if (!plan.SupportsSecretsManager)
        {
            return false;
        }

        var hasCoupon = customer.Discount?.Coupon?.Id == StripeConstants.CouponIDs.SecretsManagerStandalone;

        if (!hasCoupon)
        {
            return false;
        }

        var subscriptionProductIds = subscription.Items.Data.Select(item => item.Plan.ProductId);

        var couponAppliesTo = customer.Discount?.Coupon?.AppliesTo?.Products;

        return subscriptionProductIds.Intersect(couponAppliesTo ?? []).Any();
    }
}
