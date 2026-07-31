using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Commands;
using Bit.Core.Billing.Constants;
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
            return new BadRequest("Offer is no longer available.");
        }

        var annualLatestPlanType = AnnualUpgradeOfferPlans.ResolveAnnualLatestPlanType(organization.PlanType);
        if (annualLatestPlanType is null)
        {
            return DefaultConflict;
        }

        var subscription = await OrganizationSubscriptionHelpers.TryGetSubscriptionAsync(
            stripeAdapter, _logger, organization, CommandName,
            ["discounts.coupon", "items.data.discounts.coupon", "schedule"]);
        if (subscription is null)
        {
            return DefaultConflict;
        }

        var currentPlan = await pricingClient.GetPlanOrThrow(organization.PlanType);
        var annualLatestPlan = await pricingClient.GetPlanOrThrow(annualLatestPlanType.Value);

        var eligibility = AnnualUpgradeEligibilityMapper.Map(subscription, currentPlan, annualLatestPlan);
        if (!eligibility.IsEligible)
        {
            return Ineligible(organization, subscription, eligibility);
        }

        var phase2Items = eligibility.Lines
            .Select(line => new SubscriptionSchedulePhaseItemOptions
            {
                Price = line.TargetPriceId,
                Quantity = line.Item.Quantity,
                Discounts = ItemDiscounts(line.Item)
            })
            .ToList();

        // Eligibility above already excluded Unexpanded, Foreign, and AnnualUpgrade ownership; only None and PriceMigration remain.
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

        await priceIncreaseScheduler.ReleaseSchedule(scheduleToRelease, organization.Id);

        var schedule = await stripeAdapter.CreateSubscriptionScheduleAsync(
            new SubscriptionScheduleCreateOptions { FromSubscription = subscription.Id });

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
                Items = [.. phase1.Items.Select(i =>
                {
                    var itemDiscounts = i.Discounts is { Count: > 0 } ?
                        i.Discounts.Select(d => new SubscriptionSchedulePhaseItemDiscountOptions
                        {
                            Coupon = d.CouponId
                        }).ToList() : null;

                    return new SubscriptionSchedulePhaseItemOptions
                    {
                        Price = i.PriceId,
                        Quantity = i.Quantity,
                        Discounts = itemDiscounts
                    };
                })],
                Discounts = phase1.Discounts is { Count: > 0 } ?
                [
                    .. phase1.Discounts.Select(d => new SubscriptionSchedulePhaseDiscountOptions
                    {
                        Coupon = d.CouponId
                    })
                ] : null,
                // Only the marker's presence is read; the value is for triage.
                Metadata = new Dictionary<string, string> { [MetadataKeys.AnnualUpgrade] = sourcePlanType },
                ProrationBehavior = ProrationBehavior.None
            };

            // Customer and subscription discounts never stack, so carry the subscription's own and
            // otherwise leave it unspecified to inherit the customer's. The quote models a subset
            // of this on purpose: it counts only forever coupons.
            var phase2Discounts = subscription.Discounts?
                .Where(discount => !string.IsNullOrEmpty(discount?.Coupon?.Id))
                .Select(discount => new SubscriptionSchedulePhaseDiscountOptions
                {
                    Coupon = discount.Coupon.Id
                })
                .ToList();

            // Stripe requires every phase to be bounded (end_date or duration); Phase 2 runs
            // exactly one annual term, then the schedule releases per EndBehavior below.
            var phase2Options = new SubscriptionSchedulePhaseOptions
            {
                StartDate = phase1.EndDate,
                EndDate = phase1.EndDate.AddYears(1),
                Items = phase2Items,
                Discounts = phase2Discounts is { Count: > 0 } ? phase2Discounts : null,
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

        _logger.LogInformation(
            "{Command}: Created annual-upgrade schedule ({ScheduleId}) for Organization ({OrganizationId}): {SourcePlanType} -> {TargetPlanType}",
            CommandName, schedule.Id, organization.Id, organization.PlanType, annualLatestPlan.Type);

        return new None();
    });

    // An item-bound coupon does not travel with the customer or subscription discounts.
    // Out-of-scope coupons are accepted and applied as zero, so copying can only help.
    private static List<SubscriptionSchedulePhaseItemDiscountOptions>? ItemDiscounts(SubscriptionItem item)
    {
        var discounts = item.Discounts?
            .Where(discount => !string.IsNullOrEmpty(discount?.Coupon?.Id))
            .Select(discount => new SubscriptionSchedulePhaseItemDiscountOptions { Coupon = discount!.Coupon.Id })
            .ToList();

        return discounts is { Count: > 0 } ? discounts : null;
    }

    private BillingCommandResult<None> Ineligible(
        Organization organization, Subscription subscription, AnnualUpgradeEligibility eligibility)
    {
        switch (eligibility.Reason)
        {
            case AnnualUpgradeIneligibleReason.UnexpandedDiscounts:
            case AnnualUpgradeIneligibleReason.UnexpandedSchedule:
                _logger.LogError(
                    "{Command}: Subscription ({SubscriptionId}) for Organization ({OrganizationId}) was loaded without the expansions this command requires ({Reason}); refusing to rebuild its schedule",
                    CommandName, subscription.Id, organization.Id, eligibility.Reason);
                return DefaultConflict;

            case AnnualUpgradeIneligibleReason.ForeignSchedule:
                _logger.LogWarning(
                    "{Command}: Refusing to release unrecognized schedule ({ScheduleId}) on subscription ({SubscriptionId}) for Organization ({OrganizationId}); phase metadata keys present: {MetadataKeys}",
                    CommandName, subscription.ScheduleId, subscription.Id, organization.Id,
                    string.Join(", ", SubscriptionScheduleOwnershipMapper.DistinctPhaseMetadataKeys(subscription.Schedule)));
                return DefaultConflict;

            case AnnualUpgradeIneligibleReason.AlreadyScheduled:
                _logger.LogInformation(
                    "{Command}: Organization ({OrganizationId}) already redeemed the annual upgrade offer",
                    CommandName, organization.Id);
                return new BadRequest("Offer is no longer available.");

            case AnnualUpgradeIneligibleReason.UnmappableLine:
                _logger.LogWarning(
                    "{Command}: Subscription ({SubscriptionId}) line item price ({PriceId}) has no annual-latest mapping for Organization ({OrganizationId})",
                    CommandName, subscription.Id, eligibility.UnmappablePriceId, organization.Id);
                return new BadRequest("Offer is no longer available.");

            default:
                return new BadRequest("Offer is no longer available.");
        }
    }
}
