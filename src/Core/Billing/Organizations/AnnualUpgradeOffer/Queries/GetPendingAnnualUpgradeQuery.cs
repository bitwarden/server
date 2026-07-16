using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Organizations.AnnualUpgradeOffer.Models;
using Bit.Core.Billing.Organizations.Helpers;
using Bit.Core.Billing.Organizations.PlanMigration.Queries;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Services;
using Bit.Core.Services;
using Microsoft.Extensions.Logging;
using Stripe;

namespace Bit.Core.Billing.Organizations.AnnualUpgradeOffer.Queries;

using static StripeConstants;

public class GetPendingAnnualUpgradeQuery(
    ILogger<GetPendingAnnualUpgradeQuery> logger,
    IFeatureService featureService,
    IGetChurnOfferCohortMembershipQuery getChurnOfferCohortMembershipQuery,
    IPricingClient pricingClient,
    IStripeAdapter stripeAdapter) : IGetPendingAnnualUpgradeQuery
{
    public async Task<PendingAnnualUpgrade?> Run(Organization organization)
    {
        // Shares the price-migration program flag with the offer query (same kill switch).
        if (!featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration))
        {
            return null;
        }

        // Membership in a churn-offer-eligible cohort excludes the annual-upgrade path entirely.
        var membership = await getChurnOfferCohortMembershipQuery.Run(organization);
        if (membership is not null)
        {
            return null;
        }

        // Only monthly Teams/Enterprise vintages map to an annual-latest plan.
        var annualLatestPlanType = AnnualUpgradeOfferPlans.ResolveAnnualLatestPlanType(organization.PlanType);
        if (annualLatestPlanType is null)
        {
            return null;
        }

        if (string.IsNullOrEmpty(organization.GatewaySubscriptionId))
        {
            return null;
        }

        var subscription = await OrganizationSubscriptionHelpers.TryGetSubscriptionAsync(
            stripeAdapter, logger, organization, nameof(GetPendingAnnualUpgradeQuery), ["test_clock"]);
        if (subscription is null || subscription.Status != SubscriptionStatus.Active)
        {
            return null;
        }

        var annualLatestPlan = await pricingClient.GetPlanOrThrow(annualLatestPlanType.Value);

        // Fail-closed on any Stripe error from here on: this query runs inline on page load
        // (see GetPendingAnnualUpgradeQuery consumers), so a schedule/price lookup failure must
        // degrade to "no pending upgrade" rather than 500 the page.
        try
        {
            var schedules = await stripeAdapter.ListSubscriptionSchedulesAsync(
                new SubscriptionScheduleListOptions { Customer = subscription.CustomerId });

            // Redeemed marker: an active schedule for this subscription whose phases contain the
            // annual-latest seat price (mirrors GetAnnualUpgradeOfferQuery's alreadyRedeemed check).
            var activeSchedule = schedules.Data.FirstOrDefault(schedule =>
                schedule.SubscriptionId == subscription.Id &&
                schedule.Status == SubscriptionScheduleStatus.Active &&
                schedule.Phases.Any(phase =>
                    phase.Items.Any(item => item.PriceId == annualLatestPlan.PasswordManager.StripeSeatPlanId)));

            if (activeSchedule is not { Phases.Count: > 0 })
            {
                return null;
            }

            var now = subscription.TestClock?.FrozenTime ?? DateTime.UtcNow;

            // Earliest future phase (the post-renewal annual phase); StartDate is the renewal date.
            var upcomingPhase = activeSchedule.Phases
                .Where(phase => phase.StartDate > now)
                .MinBy(phase => phase.StartDate);

            if (upcomingPhase is null)
            {
                return null;
            }

            var lineItems = new List<PendingAnnualUpgradeLineItem>();
            foreach (var item in upcomingPhase.Items)
            {
                var price = await stripeAdapter.GetPriceAsync(item.PriceId, null);

                lineItems.Add(new PendingAnnualUpgradeLineItem
                {
                    Name = price.Nickname,
                    Amount = (price.UnitAmountDecimal ?? 0) / 100M,
                    Quantity = (int)item.Quantity,
                    Interval = price.Recurring?.Interval,
                    ProductId = price.ProductId,
                    AddonSubscriptionItem = price.Metadata != null &&
                        price.Metadata.TryGetValue("isAddOn", out var value) &&
                        bool.TryParse(value, out var isAddOn) && isAddOn
                });
            }

            return new PendingAnnualUpgrade
            {
                Plan = annualLatestPlan,
                LineItems = lineItems,
                EffectiveDate = upcomingPhase.StartDate
            };
        }
        catch (StripeException stripeException)
        {
            logger.LogWarning(
                "{Query}: Could not resolve pending annual upgrade for Organization ({OrganizationId}) | Code = {Code}",
                nameof(GetPendingAnnualUpgradeQuery), organization.Id, stripeException.StripeError?.Code);
            return null;
        }
    }
}
