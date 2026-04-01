using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Subscriptions.Entities;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Stripe;
using StripeProductIDs = Bit.Core.Billing.Constants.StripeConstants.ProductIDs;

namespace Bit.Core.Billing.Services.DiscountAudienceFilters;

/// <summary>
/// Restricts a discount to users who have never held a Bitwarden subscription.
/// </summary>
public class UserHasNoPreviousSubscriptionsFilter : IDiscountAudienceFilter
{
    private readonly IOrganizationUserRepository organizationUserRepository;
    private readonly IStripeAdapter stripeAdapter;
    private readonly IPricingClient pricingClient;

    // Caches to avoid redundant checks for users during the same request.
    private readonly Dictionary<Guid, bool> _premiumEligibilityByUser = new();
    private readonly Dictionary<Guid, bool> _familiesOrgOwnershipByUser = new();

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

        if (IsApplicableToProduct(discount, StripeProductIDs.Premium))
        {
            eligibleTiers[DiscountTierType.Premium] = await IsUserEligibleForPremiumDiscount(user);
        }

        if (IsApplicableToProduct(discount, StripeProductIDs.Families))
        {
            eligibleTiers[DiscountTierType.Families] = !await IsUserOwnerOfFamiliesOrgAsync(user);
        }

        return eligibleTiers;
    }

    /**
     * Determines if the discount is applicable to the given product based on the discount's configured Stripe product IDs.
     * If the discount does not specify any product IDs, it is considered applicable to all products.
     */
    private bool IsApplicableToProduct(SubscriptionDiscount discount, string productId) =>
        discount.StripeProductIds?.Contains(productId) ?? true;

    private async Task<bool> IsUserEligibleForPremiumDiscount(User user)
    {
        if (_premiumEligibilityByUser.TryGetValue(user.Id, out var cached))
        {
            return cached;
        }

        bool result;

        if (user.Premium)
        {
            result = false;
        }
        else if (string.IsNullOrWhiteSpace(user.GatewayCustomerId))
        {
            result = true;
        }
        else
        {
            result = !await UserHasPreviousPremiumSubscriptionAsync(user);
        }

        return _premiumEligibilityByUser[user.Id] = result;
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
                Expand = ["data.items.data.price"],
                Status = "all"
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
        if (_familiesOrgOwnershipByUser.TryGetValue(user.Id, out var cached))
        {
            return cached;
        }

        var orgDetails = await organizationUserRepository.GetManyDetailsByUserAsync(
            user.Id,
            OrganizationUserStatusType.Confirmed);

        var result = orgDetails.Any(o =>
            o.Type == OrganizationUserType.Owner &&
            IsFamiliesPlanType(o.PlanType));

        return _familiesOrgOwnershipByUser[user.Id] = result;
    }

    private static bool IsFamiliesPlanType(PlanType planType) =>
        planType is PlanType.FamiliesAnnually
            or PlanType.FamiliesAnnually2019
            or PlanType.FamiliesAnnually2025;
}
