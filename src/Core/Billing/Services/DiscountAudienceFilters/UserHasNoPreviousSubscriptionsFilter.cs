using Bit.Core.Billing.Constants;
using StripeProductIDs = Bit.Core.Billing.Constants.StripeConstants.ProductIDs;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Subscriptions.Entities;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Stripe;

namespace Bit.Core.Billing.Services.DiscountAudienceFilters;

/// <summary>
/// Restricts a discount to users who have never held a Bitwarden subscription.
/// </summary>
public class UserHasNoPreviousSubscriptionsFilter : IDiscountAudienceFilter
{
    private readonly IOrganizationUserRepository organizationUserRepository;
    private readonly IStripeAdapter stripeAdapter;
    private readonly IPricingClient pricingClient;

    public UserHasNoPreviousSubscriptionsFilter(
        IStripeAdapter stripeAdapter,
        IOrganizationUserRepository organizationUserRepository,
        IPricingClient pricingClient)
    {
        this.organizationUserRepository = organizationUserRepository;
        this.stripeAdapter = stripeAdapter;
        this.pricingClient = pricingClient;
    }

    public DiscountAudienceType SupportedType => DiscountAudienceType.UserHasNoPreviousSubscriptions;

    public async Task<IDictionary<DiscountTierType, bool>> IsUserEligible(User user, SubscriptionDiscount discount)
    {
        var eligibleTiers = Utilities.GetTierEligibilityDictionary();

        if (!DiscountContainsProductIds(discount))
        {
            // If no product IDs are specified, the discount applies to all tiers, so we check both eligibility conditions
            eligibleTiers[DiscountTierType.Premium] = await IsUserEligibleForPremiumDiscount(user);
            eligibleTiers[DiscountTierType.Families] = !await IsUserOwnerOfFamiliesOrgAsync(user);
            return eligibleTiers;
        }

        if (IsApplicableToPremiumProduct(discount))
        {
            eligibleTiers[DiscountTierType.Premium] = await IsUserEligibleForPremiumDiscount(user);
        }

        if (IsApplicableToFamiliesProduct(discount))
        {
            eligibleTiers[DiscountTierType.Families] = !await IsUserOwnerOfFamiliesOrgAsync(user);
        }

        return eligibleTiers;
    }

    private bool DiscountContainsProductIds(SubscriptionDiscount discount) =>
        discount.StripeProductIds?.Any() ?? false;

    private bool IsApplicableToFamiliesProduct(SubscriptionDiscount discount) =>
        discount.StripeProductIds?.Contains(StripeProductIDs.Families) ?? false;

    private bool IsApplicableToPremiumProduct(SubscriptionDiscount discount) =>
        discount.StripeProductIds?.Contains(StripeProductIDs.Premium) ?? false;

    private async Task<bool> IsUserEligibleForPremiumDiscount(User user)
    {
        if (user.Premium)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(user.GatewayCustomerId))
        {
            return true;
        }

        return !await UserHasPreviousPremiumSubscriptionAsync(user);
    }

    private async Task<bool> UserHasPreviousPremiumSubscriptionAsync(User user)
    {
        var premiumPlans = await pricingClient.ListPremiumPlans();
        var premiumPriceIds = premiumPlans.Select(p => p.Seat.StripePriceId).ToHashSet();
        try
        {
            var subscriptions = await stripeAdapter.ListSubscriptionsAsync(new SubscriptionListOptions
            {
                Customer = user.GatewayCustomerId,
                Expand = ["data.items.data.price"]
            });
            return subscriptions.Data.Any(subscription =>
                subscription.Items.Data.Any(item => premiumPriceIds.Contains(item.Price.Id)));
        }
        catch (StripeException ex) when (ex.StripeError.Code == StripeConstants.ErrorCodes.ResourceMissing)
        {
            // If the customer ID does not exist in Stripe, treat as no previous subscriptions
            return false;
        }
    }

    private async Task<bool> IsUserOwnerOfFamiliesOrgAsync(User user)
    {
        var orgDetails = await organizationUserRepository.GetManyDetailsByUserAsync(
            user.Id,
            OrganizationUserStatusType.Confirmed);

        return orgDetails.Any(o =>
            o.Type == OrganizationUserType.Owner &&
            IsFamiliesPlanType(o.PlanType));
    }

    private static bool IsFamiliesPlanType(PlanType planType) =>
        planType is PlanType.FamiliesAnnually
            or PlanType.FamiliesAnnually2019
            or PlanType.FamiliesAnnually2025;
}
