using Bit.Core.Billing.Extensions;
using Bit.Core.Billing.Services;
using Microsoft.Extensions.Logging;
using Stripe;
using static Bit.Core.Billing.Constants.StripeConstants;

namespace Bit.Core.Billing.Subscriptions.Schedules;

/// <summary>
/// Creates the subscription schedules our code manages. Every schedule our code creates goes through here, so each
/// one is marked with the part of our code that owns it and can be told apart from schedules created by hand in the
/// Stripe Dashboard.
/// </summary>
public interface ISubscriptionScheduleCreator
{
    /// <summary>
    /// Creates a two-phase schedule from <paramref name="subscription"/>: phase 1 mirrors the subscription as
    /// Stripe copied it, including any trial, phase 2 is <paramref name="phase2Options"/>, and the schedule releases after phase 2.
    /// Stripe rejects metadata, phases, and end behavior on a create that uses <c>from_subscription</c>, so they
    /// are applied by the update that follows. Releases the schedule and rethrows if that update fails.
    /// </summary>
    /// <param name="subscription">The subscription, loaded with <c>discounts</c> expanded.</param>
    /// <param name="phase2Options">The phase that follows the subscription's current period.</param>
    /// <param name="managingSystem">One of <see cref="ManagingSystems"/>.</param>
    /// <param name="phaseMetadata">Metadata for both phases, or null for none.</param>
    Task<SubscriptionSchedule> CreateWithPhasesAsync(
        Subscription subscription,
        SubscriptionSchedulePhaseOptions phase2Options,
        string managingSystem,
        Dictionary<string, string>? phaseMetadata = null);
}

public class SubscriptionScheduleCreator(
    IStripeAdapter stripeAdapter,
    ILogger<SubscriptionScheduleCreator> logger) : ISubscriptionScheduleCreator
{
    public async Task<SubscriptionSchedule> CreateWithPhasesAsync(
        Subscription subscription,
        SubscriptionSchedulePhaseOptions phase2Options,
        string managingSystem,
        Dictionary<string, string>? phaseMetadata = null)
    {
        var schedule = await stripeAdapter.CreateSubscriptionScheduleAsync(
            new SubscriptionScheduleCreateOptions { FromSubscription = subscription.Id });

        try
        {
            return await stripeAdapter.UpdateSubscriptionScheduleAsync(schedule.Id,
                BuildConfiguringUpdate(schedule, subscription, phase2Options, managingSystem, phaseMetadata));
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failed to update subscription schedule ({ScheduleId}) for subscription ({SubscriptionId}) managed by ({ManagingSystem}), attempting to release orphaned schedule",
                schedule.Id, subscription.Id, managingSystem);

            try
            {
                await stripeAdapter.ReleaseSubscriptionScheduleAsync(schedule.Id);
            }
            catch (Exception releaseEx)
            {
                logger.LogError(releaseEx,
                    "Failed to release orphaned subscription schedule ({ScheduleId}) for subscription ({SubscriptionId}) managed by ({ManagingSystem}). Manual release required.",
                    schedule.Id, subscription.Id, managingSystem);
            }

            throw;
        }
    }

    private static SubscriptionScheduleUpdateOptions BuildConfiguringUpdate(
        SubscriptionSchedule createdSchedule,
        Subscription subscription,
        SubscriptionSchedulePhaseOptions phase2Options,
        string managingSystem,
        Dictionary<string, string>? phaseMetadata)
    {
        var phase1 = createdSchedule.Phases[0];

        // Phase 1 must round-trip its trial and discounts. Omitting either is accepted by Stripe and
        // silently applies to the live subscription: a dropped trial ends immediately and invoices a new period.
        var phase1Options = new SubscriptionSchedulePhaseOptions
        {
            StartDate = phase1.StartDate,
            EndDate = phase1.EndDate,
            TrialEnd = phase1.TrialEnd,
            Items = [.. phase1.Items.Select(i => new SubscriptionSchedulePhaseItemOptions
            {
                Price = i.PriceId,
                Quantity = i.Quantity,
                Discounts = DiscountExtensions.BuildPhaseItemLevelDiscounts(
                    i.Discounts?.Select(d => d.CouponId) ?? [])
            })],
            Discounts = DiscountExtensions.BuildCurrentPhaseDiscounts(subscription),
            ProrationBehavior = ProrationBehavior.None
        };

        if (phaseMetadata is not null)
        {
            phase1Options.Metadata = phaseMetadata;
            phase2Options.Metadata = phaseMetadata;
        }

        return new SubscriptionScheduleUpdateOptions
        {
            EndBehavior = SubscriptionScheduleEndBehavior.Release,
            Phases = [phase1Options, phase2Options],
            Metadata = new Dictionary<string, string> { [MetadataKeys.ManagingSystem] = managingSystem }
        };
    }
}
