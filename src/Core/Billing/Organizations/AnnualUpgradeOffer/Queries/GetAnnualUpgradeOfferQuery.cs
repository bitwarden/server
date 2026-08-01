using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Organizations.AnnualUpgradeOffer.Models;
using Bit.Core.Billing.Organizations.Helpers;
using Bit.Core.Billing.Organizations.PlanMigration.Queries;
using Bit.Core.Billing.Organizations.Schedules;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Services;
using Microsoft.Extensions.Logging;
using Stripe;

namespace Bit.Core.Billing.Organizations.AnnualUpgradeOffer.Queries;

public class GetAnnualUpgradeOfferQuery(
    ILogger<GetAnnualUpgradeOfferQuery> logger,
    IGetChurnOfferCohortMembershipQuery getChurnOfferCohortMembershipQuery,
    IPricingClient pricingClient,
    IStripeAdapter stripeAdapter) : IGetAnnualUpgradeOfferQuery
{
    private static readonly List<string> SubscriptionExpansions =
    [
        "schedule",
        "customer",
        "customer.discount.coupon",
        "discounts.coupon",
        "items.data.discounts.coupon"
    ];

    public async Task<AnnualUpgradeOfferResult?> Run(Organization organization)
    {
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
            stripeAdapter, logger, organization, nameof(GetAnnualUpgradeOfferQuery), SubscriptionExpansions,
            LogLevel.Warning);
        if (subscription is null)
        {
            return null;
        }

        var eligibility = AnnualUpgradeEligibilityMapper.Map(subscription, currentPlan, annualLatestPlan);
        if (!eligibility.IsEligible)
        {
            LogIneligible(organization, subscription, eligibility);
            return null;
        }

        var previewRequests = AnnualUpgradeSavingsCalculator.BuildPreviewRequests(subscription, eligibility.Lines);

        AnnualUpgradeSavings? savings;
        try
        {
            // Two previews rather than one, because the monthly side has to be priced under the
            // same explicit coupon set as the annual one. Reading the natural upcoming invoice for
            // the monthly side would save a call but surrender control of that set and let
            // proration and one-off invoice items into the figure.
            var monthlyPreview = await stripeAdapter.CreateInvoicePreviewAsync(previewRequests.Monthly);
            var annualPreview = await stripeAdapter.CreateInvoicePreviewAsync(previewRequests.Annual);
            savings = AnnualUpgradeSavingsCalculator.SavingsFromPreviews(monthlyPreview, annualPreview);
        }
        catch (Exception exception)
        {
            // Same posture as an unmappable line: no offer beats a wrong dollar figure.
            logger.LogError(exception,
                "{Query}: Failed to preview the annual upgrade invoices for Organization ({OrganizationId}) on subscription ({SubscriptionId}); suppressing the annual upgrade offer",
                nameof(GetAnnualUpgradeOfferQuery), organization.Id, subscription.Id);
            return null;
        }

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

    private void LogIneligible(
        Organization organization, Subscription subscription, AnnualUpgradeEligibility eligibility)
    {
        switch (eligibility.Reason)
        {
            case AnnualUpgradeIneligibleReason.UnusableDiscounts:
                logger.LogError(
                    "{Query}: Subscription ({SubscriptionId}) for Organization ({OrganizationId}) has an unexpanded or couponless discount; refusing to quote a savings figure",
                    nameof(GetAnnualUpgradeOfferQuery), subscription.Id, organization.Id);
                break;

            case AnnualUpgradeIneligibleReason.UnexpandedSchedule:
                logger.LogError(
                    "{Query}: Subscription ({SubscriptionId}) for Organization ({OrganizationId}) reports schedule ({ScheduleId}) but it was not expanded; suppressing the annual upgrade offer",
                    nameof(GetAnnualUpgradeOfferQuery), subscription.Id, organization.Id, subscription.ScheduleId);
                break;

            case AnnualUpgradeIneligibleReason.AlreadyScheduled:
                logger.LogInformation(
                    "{Query}: Organization ({OrganizationId}) already redeemed the annual upgrade offer; suppressing",
                    nameof(GetAnnualUpgradeOfferQuery), organization.Id);
                break;

            case AnnualUpgradeIneligibleReason.ForeignSchedule:
                logger.LogWarning(
                    "{Query}: Organization ({OrganizationId}) has an unrecognized schedule ({ScheduleId}) on subscription ({SubscriptionId}); phase metadata keys present: {MetadataKeys}; suppressing the annual upgrade offer",
                    nameof(GetAnnualUpgradeOfferQuery), organization.Id, subscription.ScheduleId, subscription.Id,
                    string.Join(", ", SubscriptionScheduleOwnershipMapper.DistinctPhaseMetadataKeys(subscription.Schedule)));
                break;

            case AnnualUpgradeIneligibleReason.UnmappableLine:
                logger.LogWarning(
                    "{Query}: Subscription ({SubscriptionId}) line item price ({PriceId}) has no annual-latest mapping for Organization ({OrganizationId}); suppressing the annual upgrade offer",
                    nameof(GetAnnualUpgradeOfferQuery), subscription.Id, eligibility.UnmappablePriceId, organization.Id);
                break;

            default:
                logger.LogError(
                    "{Query}: Subscription ({SubscriptionId}) for Organization ({OrganizationId}) is ineligible for an unhandled reason ({Reason}); suppressing the annual upgrade offer",
                    nameof(GetAnnualUpgradeOfferQuery), subscription.Id, organization.Id, eligibility.Reason);
                break;
        }
    }
}
