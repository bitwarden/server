using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Commands;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Extensions;
using Bit.Core.Billing.Organizations.AnnualUpgradeOffer.Queries;
using Bit.Core.Billing.Organizations.Helpers;
using Bit.Core.Billing.Organizations.PlanMigration;
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
            stripeAdapter, _logger, organization, CommandName, ["customer", "discounts.coupon"]);
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

            phase2Items.Add(new SubscriptionSchedulePhaseItemOptions
            {
                Price = targetPriceId,
                Quantity = item.Quantity
            });
        }

        // Only one active schedule is allowed per Stripe subscription. Release any existing
        // schedule (e.g. a Track A price-migration schedule) before creating the annual-switch
        // schedule -- per Alex/Micah's 2026-07-02 confirmation, the organization migrates
        // straight to the annual-latest plan instead of whatever the released schedule was
        // going to do. Passing organizationId also drops the org's cohort assignment row --
        // Alex was explicit this should happen (not just be implicitly blocked by the
        // schedule-existence guard), noting the org may lose a proactive migration discount.
        await priceIncreaseScheduler.Release(subscription.CustomerId, subscription.Id, organization.Id);

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
                Discounts = phase1.Discounts is null ? null :
                [
                    .. phase1.Discounts.Select(d => new SubscriptionSchedulePhaseDiscountOptions
                    {
                        Coupon = d.CouponId
                    })
                ],
                ProrationBehavior = ProrationBehavior.None
            };

            // Phase-level discounts override the customer-level one, so the customer coupon
            // must be copied in explicitly to keep stacking; the merge de-duplicates.
            var phase2Discounts = (subscription.Customer?.Discount).MergeDiscountCouponIds(
                subscription.Discounts?.Select(d => d.Coupon.Id)).ToPhaseDiscountOptions();

            // Stripe requires every phase to be bounded (end_date or duration); Phase 2 runs
            // exactly one annual term, then the schedule releases per EndBehavior below.
            var phase2Options = new SubscriptionSchedulePhaseOptions
            {
                StartDate = phase1.EndDate,
                EndDate = phase1.EndDate.AddYears(1),
                Items = phase2Items,
                Discounts = phase2Discounts.Count > 0 ? phase2Discounts : null,
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
