using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Models;
using Bit.Core.Repositories;
using Bit.Core.Utilities;
using Stripe;

namespace Bit.Core.Billing.Queries.Implementations;

public class OrganizationBillingQueries(
    IOrganizationRepository organizationRepository,
    ISubscriberQueries subscriberQueries) : IOrganizationBillingQueries
{
    public async Task<OrganizationMetadataDTO> GetMetadata(Guid organizationId)
    {
        var organization = await organizationRepository.GetByIdAsync(organizationId);

        if (organization == null)
        {
            return null;
        }

        var customer = await subscriberQueries.GetCustomer(organization, new CustomerGetOptions
        {
            Expand = ["discount.coupon.applies_to"]
        });

        var subscription = await subscriberQueries.GetSubscription(organization);

        if (customer == null || subscription == null)
        {
            return OrganizationMetadataDTO.Default();
        }

        var isOnSecretsManagerStandalone = IsOnSecretsManagerStandalone(organization, customer, subscription);

        return new OrganizationMetadataDTO(isOnSecretsManagerStandalone);
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
