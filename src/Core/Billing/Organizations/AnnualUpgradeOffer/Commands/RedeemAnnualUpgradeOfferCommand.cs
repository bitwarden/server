using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Commands;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Organizations.AnnualUpgradeOffer.Queries;
using Bit.Core.Billing.Organizations.Helpers;
using Bit.Core.Billing.Organizations.PlanMigration;
using Bit.Core.Billing.Organizations.Schedules.Enums;
using Bit.Core.Billing.Organizations.Schedules.Queries;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Services;
using Microsoft.Extensions.Logging;
using OneOf.Types;
using Stripe;

namespace Bit.Core.Billing.Organizations.AnnualUpgradeOffer.Commands;

using static StripeConstants;

public class RedeemAnnualUpgradeOfferCommand(
    ILogger<RedeemAnnualUpgradeOfferCommand> logger,
    IGetAnnualUpgradeOfferQuery getOfferQuery,
    IGetOrganizationSubscriptionScheduleOwnershipQuery getScheduleOwnershipQuery,
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
        // Re-validate eligibility through the same query the GET endpoint uses.
        var offer = await getOfferQuery.Run(organization);
        if (offer is null)
        {
            return new BadRequest("Offer is no longer available.");
        }

        var annualLatestPlanType = AnnualUpgradeOfferPlans.ResolveAnnualLatestPlanType(organization.PlanType);
        if (annualLatestPlanType is null)
        {
            return DefaultConflict;
        }

        var subscription = await OrganizationSubscriptionHelpers.TryGetSubscriptionAsync(
            stripeAdapter, _logger, organization, CommandName,
            ["customer", "discounts.coupon", "items.data.discounts.coupon", "schedule"]);
        if (subscription is null)
        {
            return DefaultConflict;
        }

        // Stripe.NET deserializes an unexpanded "discounts" array as a list of null entries;
        // proceeding would silently drop the organization's pre-existing discounts.
        if (subscription.Discounts is { Count: > 0 } && subscription.Discounts.Any(d => d == null))
        {
            _logger.LogError(
                "{Command}: Subscription ({SubscriptionId}) for Organization ({OrganizationId}) was loaded without expanding 'discounts'; refusing to rebuild its schedule",
                CommandName, subscription.Id, organization.Id);
            return DefaultConflict;
        }

        if (subscription.Items.Data.Any(item =>
                item.Discounts is { Count: > 0 } && item.Discounts.Any(d => d is null)))
        {
            _logger.LogError(
                "{Command}: Subscription ({SubscriptionId}) for Organization ({OrganizationId}) was loaded without expanding item discounts; refusing to rebuild its schedule",
                CommandName, subscription.Id, organization.Id);
            return DefaultConflict;
        }

        var currentPlan = await pricingClient.GetPlanOrThrow(organization.PlanType);
        var annualLatestPlan = await pricingClient.GetPlanOrThrow(annualLatestPlanType.Value);

        // Map every line item to its annual-latest price before any mutation: a redemption
        // that cannot be fully mapped must fail while the organization's existing schedule
        // and cohort assignment are still intact.
        var phase2Items = new List<SubscriptionSchedulePhaseItemOptions>();
        foreach (var item in subscription.Items.Data)
        {
            var targetPriceId = OrganizationPlanMigrationPriceMapper.MapOrNull(item.Price.Id, currentPlan, annualLatestPlan);
            if (targetPriceId is null)
            {
                _logger.LogWarning(
                    "{Command}: Subscription ({SubscriptionId}) line item price ({PriceId}) has no annual-latest mapping for Organization ({OrganizationId})",
                    CommandName, subscription.Id, item.Price.Id, organization.Id);
                return DefaultConflict;
            }

            // A coupon bound to a single line does not travel with the customer or subscription
            // discounts, so it has to be copied onto the phase item explicitly or it disappears at
            // renewal. The savings quote assumes it survives. Copy by coupon ID rather than
            // promotion code: promotion codes carrying a minimum-amount restriction cannot be
            // applied to a future schedule phase.
            var itemDiscounts = item.Discounts?
                .Where(discount => !string.IsNullOrEmpty(discount?.Coupon?.Id))
                .Select(discount => new SubscriptionSchedulePhaseItemDiscountOptions
                {
                    Coupon = discount.Coupon.Id
                })
                .ToList();

            phase2Items.Add(new SubscriptionSchedulePhaseItemOptions
            {
                Price = targetPriceId,
                Quantity = item.Quantity,
                Discounts = itemDiscounts is { Count: > 0 } ? itemDiscounts : null
            });
        }

        // Stripe permits one active schedule per subscription, so the switch can only be built by
        // releasing whatever is there. A schedule Bitwarden did not create, for example a
        // negotiated renewal built by hand in the Stripe Dashboard, must never be released:
        // unlike a seat change, this operation has no way to proceed while leaving it intact.
        // The offer query suppresses this case at page load, so reaching here means a schedule
        // appeared between the offer being shown and the redemption being submitted.
        var ownership = await getScheduleOwnershipQuery.Run(organization, subscription);
        if (ownership.Ownership == OrganizationSubscriptionScheduleOwnership.Foreign)
        {
            _logger.LogWarning(
                "{Command}: Refusing to release unrecognized schedule ({ScheduleId}) on subscription ({SubscriptionId}) for Organization ({OrganizationId})",
                CommandName, ownership.Schedule?.Id, subscription.Id, organization.Id);
            return DefaultConflict;
        }

        // Releasing a Track A price-migration schedule is intended: the organization moves straight
        // to the annual-latest plan, which reaches the same destination the migration would have.
        // Passing organizationId also drops the cohort assignment row so the organization leaves
        // the migration cohort, accepting that it may lose a proactive migration discount.
        await priceIncreaseScheduler.ReleaseSchedule(ownership.Schedule, organization.Id);

        var schedule = await stripeAdapter.CreateSubscriptionScheduleAsync(
            new SubscriptionScheduleCreateOptions { FromSubscription = subscription.Id });

        try
        {
            var phase1 = schedule.Phases[0];

            // Phase 1 is the in-flight phase; Stripe rejects the update unless it
            // round-trips unchanged, including any discounts already on it.
            var phase1Options = new SubscriptionSchedulePhaseOptions
            {
                StartDate = phase1.StartDate,
                EndDate = phase1.EndDate,
                Items = [.. phase1.Items.Select(i => new SubscriptionSchedulePhaseItemOptions
                {
                    Price = i.PriceId,
                    Quantity = i.Quantity
                })],
                Discounts = phase1.Discounts is { Count: > 0 } ?
                [
                    .. phase1.Discounts.Select(d => new SubscriptionSchedulePhaseDiscountOptions
                    {
                        Coupon = d.CouponId
                    })
                ] : null,
                ProrationBehavior = ProrationBehavior.None
            };

            // Customer-level and subscription-level discounts do not stack: Stripe only applies the
            // customer's when the subscription has none of its own. Carry the subscription's
            // coupons when it has them, and otherwise leave the array unspecified so the phase
            // inherits the customer coupon, which is exactly what the subscription bills under
            // today. Merging both would start applying a customer coupon Stripe is suppressing,
            // quietly enlarging the organization's discount at renewal.
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
}
