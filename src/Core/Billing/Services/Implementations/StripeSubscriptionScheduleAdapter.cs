using Bit.Core.Billing.Extensions;
using Microsoft.Extensions.Logging;
using Stripe;
using static Bit.Core.Billing.Constants.StripeConstants;

namespace Bit.Core.Billing.Services.Implementations;

/// <summary>
/// Stripe subscription schedule operations. <see cref="CreateSubscriptionScheduleWithPhasesAsync"/> is the only
/// way our code creates a schedule, and it marks every schedule with the part of our code that owns it so it can
/// be told apart from schedules created by hand in the Stripe Dashboard.
/// </summary>
public class StripeSubscriptionScheduleAdapter(
    SubscriptionScheduleService subscriptionScheduleService,
    ILogger<StripeSubscriptionScheduleAdapter> logger)
{
    /// <summary>
    /// Creates a two-phase schedule from <paramref name="subscription"/>: phase 1 mirrors the subscription as
    /// Stripe copied it, phase 2 is <paramref name="phase2Options"/>, and the schedule releases after phase 2.
    /// Stripe rejects metadata, phases, and end behavior on a create that uses <c>from_subscription</c>, so they
    /// are applied by the update that follows. Releases the schedule and rethrows if that update fails.
    /// </summary>
    /// <param name="subscription">The subscription, loaded with <c>discounts</c> expanded.</param>
    /// <param name="phase2Options">The phase that follows the subscription's current period.</param>
    /// <param name="managingSystem">One of <see cref="ManagingSystems"/>.</param>
    /// <param name="phaseMetadata">Metadata for both phases, or null for none.</param>
    public async Task<SubscriptionSchedule> CreateSubscriptionScheduleWithPhasesAsync(
        Subscription subscription,
        SubscriptionSchedulePhaseOptions phase2Options,
        string managingSystem,
        Dictionary<string, string>? phaseMetadata = null)
    {
        var schedule = await subscriptionScheduleService.CreateAsync(
            new SubscriptionScheduleCreateOptions { FromSubscription = subscription.Id });

        try
        {
            return await subscriptionScheduleService.UpdateAsync(schedule.Id,
                BuildConfiguringUpdate(schedule, subscription, phase2Options, managingSystem, phaseMetadata));
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failed to update subscription schedule ({ScheduleId}) for subscription ({SubscriptionId}) managed by ({ManagingSystem}), attempting to release orphaned schedule",
                schedule.Id, subscription.Id, managingSystem);

            try
            {
                await subscriptionScheduleService.ReleaseAsync(schedule.Id);
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

    public Task<SubscriptionSchedule> GetSubscriptionScheduleAsync(string id, SubscriptionScheduleGetOptions? options = null) =>
        subscriptionScheduleService.GetAsync(id, options);

    public Task<StripeList<SubscriptionSchedule>> ListSubscriptionSchedulesAsync(SubscriptionScheduleListOptions options) =>
        subscriptionScheduleService.ListAsync(options);

    public Task<SubscriptionSchedule> UpdateSubscriptionScheduleAsync(string id, SubscriptionScheduleUpdateOptions options) =>
        subscriptionScheduleService.UpdateAsync(id, options);

    public Task<SubscriptionSchedule> ReleaseSubscriptionScheduleAsync(string id, SubscriptionScheduleReleaseOptions? options = null) =>
        subscriptionScheduleService.ReleaseAsync(id, options);

    private static SubscriptionScheduleUpdateOptions BuildConfiguringUpdate(
        SubscriptionSchedule createdSchedule,
        Subscription subscription,
        SubscriptionSchedulePhaseOptions phase2Options,
        string managingSystem,
        Dictionary<string, string>? phaseMetadata)
    {
        var phase1 = createdSchedule.Phases[0];

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
