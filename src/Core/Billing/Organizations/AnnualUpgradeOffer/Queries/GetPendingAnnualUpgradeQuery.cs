using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Organizations.AnnualUpgradeOffer.Models;
using Bit.Core.Billing.Organizations.Helpers;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Services;
using Bit.Core.Exceptions;
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
        // Shares the price-migration program flag with the offer query (same kill switch).
        if (!featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration))
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

        // Fail-closed on any error from here on: this query runs inline on page load, so a
        // pricing/schedule/price lookup failure must degrade to "no pending upgrade" rather
        // than 500 the page.
        try
        {
            var annualLatestPlan = await pricingClient.GetPlanOrThrow(annualLatestPlanType.Value);

            var schedules = await stripeAdapter.ListSubscriptionSchedulesAsync(
                new SubscriptionScheduleListOptions
                {
                    Customer = subscription.CustomerId,
                    Expand = ["data.phases.items.price"]
                });

            // Redeemed marker: an active schedule for this subscription whose phases contain the
            // annual-latest seat price.
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

            var lineItems = new List<PendingAnnualUpgradeLineItem>();
            foreach (var item in upcomingPhase.Items)
            {
                var price = item.Price;

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
        catch (Exception exception) when (exception is StripeException or BillingException or NotFoundException)
        {
            logger.LogWarning(
                exception,
                "{Query}: Could not resolve pending annual upgrade for Organization ({OrganizationId})",
                nameof(GetPendingAnnualUpgradeQuery), organization.Id);
            return null;
        }
    }
}
