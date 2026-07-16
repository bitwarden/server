using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Organizations.AnnualUpgradeOffer.Models;
using Bit.Core.Billing.Organizations.PlanMigration.Queries;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Services;
using Bit.Core.Services;
using Microsoft.Extensions.Logging;
using Stripe;

namespace Bit.Core.Billing.Organizations.AnnualUpgradeOffer.Queries;

using static StripeConstants;

public class GetAnnualUpgradeOfferQuery(
    ILogger<GetAnnualUpgradeOfferQuery> logger,
    IFeatureService featureService,
    IGetChurnOfferCohortMembershipQuery getChurnOfferCohortMembershipQuery,
    IPricingClient pricingClient,
    IStripeAdapter stripeAdapter) : IGetAnnualUpgradeOfferQuery
{
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

        var subscription = await AnnualUpgradeOfferSubscriptionHelpers.TryGetSubscriptionAsync(
            stripeAdapter, logger, organization, nameof(GetAnnualUpgradeOfferQuery));
        if (subscription is null)
        {
            return null;
        }

        // A redeemed org keeps its monthly PlanType until renewal, so the annual-switch schedule
        // is the only durable marker that the offer was already taken. Only the annual-latest
        // seat price suppresses: a Track A migration schedule targets a monthly price and must
        // keep the offer visible (redeeming releases and replaces that schedule).
        var schedules = await stripeAdapter.ListSubscriptionSchedulesAsync(
            new SubscriptionScheduleListOptions { Customer = subscription.CustomerId });
        var alreadyRedeemed = schedules.Data.Any(s =>
            s.SubscriptionId == subscription.Id &&
            s.Status == SubscriptionScheduleStatus.Active &&
            s.Phases.Any(p => p.Items.Any(i => i.PriceId == annualLatestPlan.PasswordManager.StripeSeatPlanId)));
        if (alreadyRedeemed)
        {
            return null;
        }

        // Savings quote what Stripe actually bills: the purchased seat quantity on the
        // subscription's seat line, which the redemption schedule preserves and the renewal
        // invoice charges. Occupied seats can be lower and would understate both figures.
        var seatItem = subscription.Items.Data.FirstOrDefault(i =>
            i.Price?.Id == currentPlan.PasswordManager.StripeSeatPlanId);
        if (seatItem is null)
        {
            return null;
        }

        var currentAnnualCost = currentPlan.PasswordManager.SeatPrice * seatItem.Quantity * 12;
        var newAnnualCost = annualLatestPlan.PasswordManager.SeatPrice * seatItem.Quantity;
        var savings = currentAnnualCost - newAnnualCost;

        if (savings <= 0)
        {
            return null;
        }

        return new AnnualUpgradeOfferResult(currentAnnualCost, newAnnualCost, savings);
    }
}
