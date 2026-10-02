using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Organizations.AnnualUpgradeOffer.Models;
using Bit.Core.Billing.Organizations.Helpers;
using Bit.Core.Billing.Organizations.PlanMigration.Queries;
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
        "customer.discount.source.coupon",
        "discounts.source.coupon",
        "items.data.discounts.source"
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
            stripeAdapter, logger, organization, SubscriptionExpansions);
        if (subscription is null)
        {
            return null;
        }

        foreach (var source in (subscription.Items?.Data ?? [])
            .SelectMany(item => item.Discounts ?? [])
            .Select(discount => discount?.Source)
            .Where(source => source is { CouponId.Length: > 0, Coupon: null }))
        {
            source!.Coupon = await TryGetCouponAsync(source.CouponId);
        }

        var lines = AnnualUpgradeLineMapper.MapOrNull(
            logger, organization.Id, subscription, currentPlan, annualLatestPlan);
        if (lines is null)
        {
            return null;
        }

        var previewRequests = AnnualUpgradeSavingsCalculator.BuildPreviewRequests(subscription, lines);

        AnnualUpgradeSavings? savings;
        try
        {
            var monthlyPreview = await stripeAdapter.CreateInvoicePreviewAsync(previewRequests.Monthly);
            var annualPreview = await stripeAdapter.CreateInvoicePreviewAsync(previewRequests.Annual);
            savings = AnnualUpgradeSavingsCalculator.SavingsFromPreviews(monthlyPreview, annualPreview);
        }
        catch (Exception exception)
        {
            // Same posture as an unmappable line: no offer beats a wrong dollar figure.
            logger.LogError(exception,
                "{Caller}: Failed to preview the annual upgrade invoices for Organization ({OrganizationId}) on subscription ({SubscriptionId}); suppressing the annual upgrade offer",
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

    private async Task<Coupon?> TryGetCouponAsync(string couponId)
    {
        try
        {
            return await stripeAdapter.GetCouponAsync(couponId, new CouponGetOptions { Expand = ["applies_to"] });
        }
        catch (StripeException stripeException)
        {
            logger.LogWarning(
                "{Caller}: Could not retrieve item-level coupon ({CouponId}) | Code = {Code}",
                nameof(GetAnnualUpgradeOfferQuery), couponId, stripeException.StripeError?.Code);
            return null;
        }
    }
}
