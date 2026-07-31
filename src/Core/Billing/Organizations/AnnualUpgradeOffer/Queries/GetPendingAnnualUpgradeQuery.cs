using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Organizations.AnnualUpgradeOffer.Models;
using Bit.Core.Billing.Organizations.Helpers;
using Bit.Core.Billing.Organizations.Schedules;
using Bit.Core.Billing.Organizations.Schedules.Enums;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Services;
using Bit.Core.Models.Business;
using Bit.Core.Services;
using Microsoft.Extensions.Logging;
using Stripe;

namespace Bit.Core.Billing.Organizations.AnnualUpgradeOffer.Queries;

using static StripeConstants;

public class GetPendingAnnualUpgradeQuery(
    ILogger<GetPendingAnnualUpgradeQuery> logger,
    IFeatureService featureService,
    IPricingClient pricingClient,
    IStripeAdapter stripeAdapter) : IGetPendingAnnualUpgradeQuery
{
    public async Task<PendingAnnualUpgrade?> Run(Organization organization)
    {
        if (!featureService.IsEnabled(FeatureFlagKeys.PM38333_AnnualBillingSavings))
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

        // Fail-closed on any error from here on: this query runs inline on page load, so a
        // pricing lookup failure must degrade to "no pending upgrade" rather
        // than 500 the page.
        try
        {
            var subscription = await OrganizationSubscriptionHelpers.TryGetSubscriptionAsync(
                stripeAdapter, logger, organization, nameof(GetPendingAnnualUpgradeQuery),
                ["test_clock", "schedule.phases.items.price"]);
            if (subscription is null || subscription.Status != SubscriptionStatus.Active)
            {
                return null;
            }

            var ownership = SubscriptionScheduleOwnershipMapper.Map(subscription);
            if (ownership == OrganizationSubscriptionScheduleOwnership.Unexpanded)
            {
                logger.LogError(
                    "{Query}: Subscription ({SubscriptionId}) for Organization ({OrganizationId}) reports schedule ({ScheduleId}) but it was not expanded; returning no pending upgrade",
                    nameof(GetPendingAnnualUpgradeQuery), subscription.Id, organization.Id, subscription.ScheduleId);
                return null;
            }

            if (ownership != OrganizationSubscriptionScheduleOwnership.AnnualUpgrade)
            {
                return null;
            }

            var annualLatestPlan = await pricingClient.GetPlanOrThrow(annualLatestPlanType.Value);

            var activeSchedule = subscription.Schedule;
            if (activeSchedule is not { Phases.Count: > 0 })
            {
                return null;
            }

            var now = subscription.TestClock?.FrozenTime ?? DateTime.UtcNow;

            // Earliest future phase that actually carries the annual-latest seat price. Past/current
            // phases are retained by Stripe for the schedule's whole life, so once the annual phase
            // is active there is no future phase and we correctly report nothing pending.
            var upcomingPhase = activeSchedule.Phases
                .Where(phase => phase.StartDate > now &&
                    phase.Items.Any(item => item.PriceId == annualLatestPlan.PasswordManager.StripeSeatPlanId))
                .MinBy(phase => phase.StartDate);

            if (upcomingPhase is null)
            {
                return null;
            }

            var lineItems = upcomingPhase.Items
                .Select(item => new SubscriptionInfo.BillingSubscription.BillingSubscriptionItem(item))
                .ToList();

            return new PendingAnnualUpgrade
            {
                Plan = annualLatestPlan,
                LineItems = lineItems,
                EffectiveDate = upcomingPhase.StartDate
            };
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "{Query}: Could not resolve pending annual upgrade for Organization ({OrganizationId})",
                nameof(GetPendingAnnualUpgradeQuery), organization.Id);
            return null;
        }
    }
}
