using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Organizations.Models;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Services;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Stripe;

namespace Bit.Core.Billing.Organizations.Queries;

public interface IGetOrganizationMetadataQuery
{
    Task<OrganizationMetadata?> Run(Organization organization);
}

public class GetOrganizationMetadataQuery(
    IGlobalSettings globalSettings,
    IOrganizationRepository organizationRepository,
    IPricingClient pricingClient,
    ISubscriberService subscriberService) : IGetOrganizationMetadataQuery
{
    public async Task<OrganizationMetadata?> Run(Organization organization)
    {
        if (globalSettings.SelfHosted)
        {
            return OrganizationMetadata.Default;
        }

        var orgOccupiedSeats = await organizationRepository.GetOccupiedSeatCountByOrganizationIdAsync(organization.Id);

        if (string.IsNullOrWhiteSpace(organization.GatewaySubscriptionId))
        {
            return OrganizationMetadata.Default with
            {
                OrganizationOccupiedSeats = orgOccupiedSeats.Total
            };
        }

        var customer = await subscriberService.GetCustomer(organization);

        var subscription = await subscriberService.GetSubscription(organization, new SubscriptionGetOptions
        {
            Expand = ["discounts.coupon.applies_to"]
        });

        if (customer == null || subscription == null)
        {
            return OrganizationMetadata.Default with
            {
                OrganizationOccupiedSeats = orgOccupiedSeats.Total
            };
        }

        var isOnSecretsManagerStandalone = await IsOnSecretsManagerStandalone(organization, customer, subscription);

        return new OrganizationMetadata(
            isOnSecretsManagerStandalone,
            orgOccupiedSeats.Total);
    }

    private async Task<bool> IsOnSecretsManagerStandalone(
        Organization organization,
        Customer? customer,
        Subscription? subscription)
    {
        if (customer == null || subscription == null)
        {
            return false;
        }

        var plan = await pricingClient.GetPlanOrThrow(organization.PlanType);

        if (!plan.SupportsSecretsManager)
        {
            return false;
        }

        var coupon = subscription.Discounts?.FirstOrDefault(discount =>
            discount.Coupon?.Id == StripeConstants.CouponIDs.SecretsManagerStandalone)?.Coupon;

        if (coupon == null)
        {
            return false;
        }

        var subscriptionProductIds = subscription.Items.Data.Select(item => item.Plan.ProductId);

        var couponAppliesTo = coupon.AppliesTo?.Products;

        return subscriptionProductIds.Intersect(couponAppliesTo ?? []).Any();
    }
}
