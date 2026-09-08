using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Commands;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Extensions;
using Bit.Core.Billing.Organizations.Helpers;
using Bit.Core.Billing.Organizations.PlanMigration.Queries;
using Bit.Core.Billing.Organizations.Schedules;
using Bit.Core.Billing.Organizations.Schedules.Enums;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Services;
using Microsoft.Extensions.Logging;
using OneOf.Types;
using Stripe;

namespace Bit.Core.Billing.Organizations.AnnualUpgradeOffer.Commands;

using static StripeConstants;

public class RedeemAnnualUpgradeOfferCommand(
    ILogger<RedeemAnnualUpgradeOfferCommand> logger,
    IGetChurnOfferCohortMembershipQuery getChurnOfferCohortMembershipQuery,
    IPriceIncreaseScheduler priceIncreaseScheduler,
    IPricingClient pricingClient,
    IStripeAdapter stripeAdapter)
    : BaseBillingCommand<RedeemAnnualUpgradeOfferCommand>(logger), IRedeemAnnualUpgradeOfferCommand
{
    private readonly ILogger<RedeemAnnualUpgradeOfferCommand> _logger = logger;

    private const string OfferNoLongerAvailable = "Offer is no longer available.";

    protected override Conflict DefaultConflict =>
        new("We had a problem switching your billing to annual. Please contact support for assistance.");

    public Task<BillingCommandResult<None>> Run(Organization organization) => HandleAsync<None>(async () =>
    {
        // Mutually exclusive with the churn-mitigation coupon offer.
        if (await getChurnOfferCohortMembershipQuery.Run(organization) is not null)
        {
            _logger.LogInformation(
                "{Command}: Organization ({OrganizationId}) is in a churn-offer cohort; refusing the annual upgrade",
                CommandName, organization.Id);
            return new BadRequest(OfferNoLongerAvailable);
        }

        var annualLatestPlanType = AnnualUpgradeOfferPlans.ResolveAnnualLatestPlanType(organization.PlanType);
        if (annualLatestPlanType is null)
        {
            return DefaultConflict;
        }

        if (string.IsNullOrEmpty(organization.GatewaySubscriptionId))
        {
            return new BadRequest(OfferNoLongerAvailable);
        }

        var subscription = await OrganizationSubscriptionHelpers.TryGetSubscriptionAsync(
            stripeAdapter, _logger, organization,
            ["discounts.source.coupon", "items.data.discounts.source", "schedule"]);
        if (subscription is null)
        {
            return new BadRequest(OfferNoLongerAvailable);
        }

        var currentPlan = await pricingClient.GetPlanOrThrow(organization.PlanType);
        var annualLatestPlan = await pricingClient.GetPlanOrThrow(annualLatestPlanType.Value);

        var lines = AnnualUpgradeLineMapper.MapOrNull(
            _logger, organization.Id, subscription, currentPlan, annualLatestPlan);
        if (lines is null)
        {
            return new BadRequest(OfferNoLongerAvailable);
        }

        var phase2Items = lines
            .Select(line => new SubscriptionSchedulePhaseItemOptions
            {
                Price = line.TargetPriceId,
                Quantity = line.Item.Quantity,
                Discounts = DiscountExtensions.BuildPhaseItemLevelDiscounts(
                    line.Item.Discounts?.Select(d => d?.Source?.CouponId) ?? [])
            })
            .ToList();

        // MapOrNull above already excluded Unexpanded, Foreign, and AnnualUpgrade ownership; only None and PriceMigration remain.
        var ownership = SubscriptionScheduleOwnershipMapper.Map(subscription);

        // Releasing a price-migration schedule is intended: annual-latest is where the migration was
        // heading anyway. Passing the organization also drops its cohort assignment row, which is
        // required even when no schedule exists, because assignment precedes scheduling.
        SubscriptionSchedule? scheduleToRelease = ownership switch
        {
            OrganizationSubscriptionScheduleOwnership.None or
                OrganizationSubscriptionScheduleOwnership.Foreign or
                OrganizationSubscriptionScheduleOwnership.Unexpanded => null,
            OrganizationSubscriptionScheduleOwnership.AnnualUpgrade or
                OrganizationSubscriptionScheduleOwnership.PriceMigration => subscription.Schedule
        };

        // Stripe permits one active schedule per subscription, so the prior schedule has to go
        // before the replacement can be created. The cohort assignment drop has no such ordering
        // constraint, so it is deferred until the new schedule is configured (below); dropping it
        // here would make a failed create unrecoverable.
        await priceIncreaseScheduler.ReleaseSchedule(scheduleToRelease);

        SubscriptionSchedule schedule;

        try
        {
            schedule = await stripeAdapter.CreateSubscriptionScheduleAsync(
                new SubscriptionScheduleCreateOptions { FromSubscription = subscription.Id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "{Command}: Failed to create annual-upgrade schedule for Organization ({OrganizationId}) after releasing schedule ({ReleasedScheduleId}). The organization keeps its migration cohort assignment, so the recovery scheduler will re-create the released schedule on the next upcoming-invoice or subscription-updated event; verify it was re-created.",
                CommandName, organization.Id, scheduleToRelease?.Id);

            throw;
        }

        try
        {
            var phase1 = schedule.Phases[0];

            var sourcePlanType = organization.PlanType.ToString();

            // Phase 1 must round-trip its discounts. Omitting them is accepted by Stripe and
            // silently strips them from the live subscription.
            var phase1Options = new SubscriptionSchedulePhaseOptions
            {
                StartDate = phase1.StartDate,
                EndDate = phase1.EndDate,
                Items = [.. phase1.Items.Select(i => new SubscriptionSchedulePhaseItemOptions
                {
                    Price = i.PriceId,
                    Quantity = i.Quantity,
                    Discounts = DiscountExtensions.BuildPhaseItemLevelDiscounts(
                        i.Discounts?.Select(d => d.CouponId) ?? [])
                })],
                Discounts = ReusedPhaseDiscounts(subscription),
                // Only the marker's presence is read; the value is for triage.
                Metadata = new Dictionary<string, string> { [MetadataKeys.AnnualUpgrade] = sourcePlanType },
                ProrationBehavior = ProrationBehavior.None
            };

            // Stripe requires every phase to be bounded (end_date or duration); Phase 2 runs
            // exactly one annual term, then the schedule releases per EndBehavior below.
            var phase2Options = new SubscriptionSchedulePhaseOptions
            {
                StartDate = phase1.EndDate,
                EndDate = phase1.EndDate.AddYears(1),
                Items = phase2Items,
                Discounts = ReusedPhaseDiscounts(subscription),
                Metadata = new Dictionary<string, string> { [MetadataKeys.AnnualUpgrade] = sourcePlanType },
                ProrationBehavior = ProrationBehavior.None
            };

            await stripeAdapter.UpdateSubscriptionScheduleAsync(schedule.Id,
                new SubscriptionScheduleUpdateOptions
                {
                    EndBehavior = SubscriptionScheduleEndBehavior.Release,
                    Phases = [phase1Options, phase2Options]
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "{Command}: Failed to configure annual-upgrade schedule ({ScheduleId}) for Organization ({OrganizationId}), attempting to release orphaned schedule",
                CommandName, schedule.Id, organization.Id);

            try
            {
                await stripeAdapter.ReleaseSubscriptionScheduleAsync(schedule.Id);
            }
            catch (StripeException releaseEx)
            {
                _logger.LogError(releaseEx,
                    "{Command}: Failed to release orphaned annual-upgrade schedule ({ScheduleId}) for Organization ({OrganizationId})",
                    CommandName, schedule.Id, organization.Id);
            }

            throw;
        }

        // Deferred from the release above: now that the annual-upgrade schedule exists and is
        // configured, the organization has left the price-migration program for good. Passing a
        // null schedule drops only the cohort assignment row.
        await priceIncreaseScheduler.ReleaseSchedule(null, organization.Id);

        _logger.LogInformation(
            "{Command}: Created annual-upgrade schedule ({ScheduleId}) for Organization ({OrganizationId}): {SourcePlanType} -> {TargetPlanType}",
            CommandName, schedule.Id, organization.Id, organization.PlanType, annualLatestPlan.Type);

        return new None();
    });

    // Reuse existing discounts (nothing re-minted); null lets Stripe inherit the customer's at renewal.
    private static List<SubscriptionSchedulePhaseDiscountOptions>? ReusedPhaseDiscounts(Subscription subscription) =>
        subscription.Discounts is { Count: > 0 }
            ? [.. subscription.Discounts.Select(discount => new SubscriptionSchedulePhaseDiscountOptions { Discount = discount.Id })]
            : null;
}
