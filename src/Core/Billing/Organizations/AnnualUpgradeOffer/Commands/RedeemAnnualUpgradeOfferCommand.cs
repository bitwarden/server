using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Commands;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Organizations.AnnualUpgradeOffer.Queries;
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
        // Re-validate eligibility through the same query the GET endpoint uses -- mirrors
        // RedeemChurnMitigationOfferCommand's re-validation pattern.
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

        var subscription = await TryGetSubscriptionAsync(organization);
        if (subscription is null)
        {
            return DefaultConflict;
        }

        // Only one active schedule is allowed per Stripe subscription. Release any existing
        // schedule (e.g. a Track A price-migration schedule) before creating the annual-switch
        // schedule -- per Alex/Micah's 2026-07-02 confirmation, the organization migrates
        // straight to the annual-latest plan instead of whatever the released schedule was
        // going to do. Passing organizationId also drops the org's cohort assignment row --
        // Alex was explicit this should happen (not just be implicitly blocked by the
        // schedule-existence guard), noting the org may lose a proactive migration discount.
        await priceIncreaseScheduler.Release(subscription.CustomerId, subscription.Id, organization.Id);

        var currentPlan = await pricingClient.GetPlanOrThrow(organization.PlanType);
        var annualLatestPlan = await pricingClient.GetPlanOrThrow(annualLatestPlanType.Value);

        var schedule = await stripeAdapter.CreateSubscriptionScheduleAsync(
            new SubscriptionScheduleCreateOptions { FromSubscription = subscription.Id });

        try
        {
            var phase1 = schedule.Phases[0];

            var phase1Options = new SubscriptionSchedulePhaseOptions
            {
                StartDate = phase1.StartDate,
                EndDate = phase1.EndDate,
                Items = [.. phase1.Items.Select(i => new SubscriptionSchedulePhaseItemOptions
                {
                    Price = i.PriceId,
                    Quantity = i.Quantity
                })],
                ProrationBehavior = ProrationBehavior.None
            };

            var phase2Options = new SubscriptionSchedulePhaseOptions
            {
                Items = [.. phase1.Items.Select(i => new SubscriptionSchedulePhaseItemOptions
                {
                    // Match the same SM-vs-PM seat price swap OrganizationBillingService.
                    // UpdateSubscriptionPlanFrequency uses for an immediate cadence change.
                    Price = i.PriceId == currentPlan.SecretsManager?.StripeSeatPlanId
                        ? annualLatestPlan.SecretsManager?.StripeSeatPlanId
                        : annualLatestPlan.PasswordManager.StripeSeatPlanId,
                    Quantity = i.Quantity
                })],
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
            catch (Exception releaseEx)
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

    private async Task<Subscription?> TryGetSubscriptionAsync(Organization organization)
    {
        try
        {
            return await stripeAdapter.GetSubscriptionAsync(organization.GatewaySubscriptionId,
                new SubscriptionGetOptions { Expand = ["customer"] });
        }
        catch (StripeException stripeException) when (stripeException.StripeError?.Code == ErrorCodes.ResourceMissing)
        {
            _logger.LogError(
                "{Command}: Subscription ({SubscriptionId}) for Organization ({OrganizationId}) was not found",
                CommandName, organization.GatewaySubscriptionId, organization.Id);
            return null;
        }
    }
}
