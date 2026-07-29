using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Organizations.AnnualUpgradeOffer.Models;
using Bit.Core.Billing.Organizations.Helpers;
using Bit.Core.Billing.Organizations.PlanMigration.Queries;
using Bit.Core.Billing.Organizations.Schedules;
using Bit.Core.Billing.Organizations.Schedules.Enums;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Services;
using Bit.Core.Services;
using Microsoft.Extensions.Logging;
using Stripe;

namespace Bit.Core.Billing.Organizations.AnnualUpgradeOffer.Queries;

public class GetAnnualUpgradeOfferQuery(
    ILogger<GetAnnualUpgradeOfferQuery> logger,
    IFeatureService featureService,
    IGetChurnOfferCohortMembershipQuery getChurnOfferCohortMembershipQuery,
    IPricingClient pricingClient,
    IStripeAdapter stripeAdapter) : IGetAnnualUpgradeOfferQuery
{
    private static readonly List<string> SubscriptionExpansions =
    [
        "schedule",
        "customer",
        "customer.discount.coupon.applies_to",
        "discounts.coupon.applies_to",
        "items.data.discounts.coupon"
    ];

    public async Task<AnnualUpgradeOfferResult?> Run(Organization organization)
    {
        // Kill switch: the offer shares the business plan migration program's flag so ops can
        // stop new redemptions without a deploy. The renewal webhook stays ungated on purpose --
        // schedules created before a flag kill still activate and must flip PlanType.
        if (!featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration))
        {
            return null;
        }

        // Mutual exclusivity with the churn-mitigation coupon offer: membership in a churn-offer
        // -eligible cohort excludes this offer entirely, regardless of whether that offer is
        // currently live (e.g. its one-shot coupon may already be consumed).
        var membership = await getChurnOfferCohortMembershipQuery.Run(organization);
        if (membership is not null)
        {
            return null;
        }

        var annualLatestPlanType = AnnualUpgradeOfferPlans.ResolveAnnualLatestPlanType(organization.PlanType);
        if (annualLatestPlanType is null)
        {
            return null;
        }

        if (string.IsNullOrEmpty(organization.GatewaySubscriptionId))
        {
            return null;
        }

        var currentPlan = await pricingClient.GetPlanOrThrow(organization.PlanType);
        var annualLatestPlan = await pricingClient.GetPlanOrThrow(annualLatestPlanType.Value);

        var subscription = await OrganizationSubscriptionHelpers.TryGetSubscriptionAsync(
            stripeAdapter, logger, organization, nameof(GetAnnualUpgradeOfferQuery), SubscriptionExpansions);
        if (subscription is null)
        {
            return null;
        }

        // Stripe.NET deserializes an unexpanded discount array as a list of null entries.
        // Quoting from one would silently price the organization as if it had no discounts.
        if (HasUnexpandedDiscounts(subscription))
        {
            logger.LogError(
                "{Query}: Subscription ({SubscriptionId}) for Organization ({OrganizationId}) was loaded with unexpanded discounts; refusing to quote a savings figure",
                nameof(GetAnnualUpgradeOfferQuery), subscription.Id, organization.Id);
            return null;
        }

        var ownership = SubscriptionScheduleOwnershipMapper.MapOrNull(subscription);
        if (ownership is null)
        {
            // A caller contract violation rather than a data condition, but this sits on the
            // cancellation dialog's page load, so the offer hides quietly instead of breaking it.
            logger.LogError(
                "{Query}: Subscription ({SubscriptionId}) for Organization ({OrganizationId}) reports schedule ({ScheduleId}) but it was not expanded; suppressing the annual upgrade offer",
                nameof(GetAnnualUpgradeOfferQuery), subscription.Id, organization.Id, subscription.ScheduleId);
            return null;
        }

        switch (ownership.Ownership)
        {
            // A redeemed organization keeps its monthly PlanType until renewal, so the annual
            // schedule is the only durable marker that the offer was already taken.
            case OrganizationSubscriptionScheduleOwnership.AnnualUpgrade:
                logger.LogInformation(
                    "{Query}: Organization ({OrganizationId}) already redeemed the annual upgrade offer; suppressing",
                    nameof(GetAnnualUpgradeOfferQuery), organization.Id);
                return null;

            // Redeeming would have to release a schedule Bitwarden did not create, for example a
            // negotiated renewal built by hand in the Stripe Dashboard. Never show an offer whose
            // redemption we would refuse. The metadata keys go in the log, keys only, because they
            // are what tells a hand-built schedule apart from one of ours that lost its marker.
            case OrganizationSubscriptionScheduleOwnership.Foreign:
                logger.LogWarning(
                    "{Query}: Organization ({OrganizationId}) has an unrecognized schedule ({ScheduleId}) on subscription ({SubscriptionId}); phase metadata keys present: {MetadataKeys}; suppressing the annual upgrade offer",
                    nameof(GetAnnualUpgradeOfferQuery), organization.Id, ownership.Schedule?.Id, subscription.Id,
                    string.Join(", ", SubscriptionScheduleOwnershipMapper.DistinctPhaseMetadataKeys(ownership.Schedule)));
                return null;
        }

        var savings = AnnualUpgradeSavingsCalculator.Calculate(subscription, currentPlan, annualLatestPlan);
        if (savings is null)
        {
            return null;
        }

        var difference = savings.Value.CurrentAnnualCost - savings.Value.NewAnnualCost;
        if (difference <= 0)
        {
            return null;
        }

        return new AnnualUpgradeOfferResult(
            savings.Value.CurrentAnnualCost, savings.Value.NewAnnualCost, difference);
    }

    private static bool HasUnexpandedDiscounts(Subscription subscription) =>
        (subscription.Discounts is { Count: > 0 } && subscription.Discounts.Any(d => d is null)) ||
        subscription.Items.Data.Any(item =>
            item.Discounts is { Count: > 0 } && item.Discounts.Any(d => d is null));
}
