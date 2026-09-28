using Bit.Core.Billing.Extensions;
using Microsoft.Extensions.Logging;
using Stripe;
using static Bit.Core.Billing.Constants.StripeConstants;

namespace Bit.Core.Billing.Services.Implementations;

public class StripeSubscriptionScheduleAdapter(
    SubscriptionScheduleService subscriptionScheduleService,
    ILogger<StripeSubscriptionScheduleAdapter> logger) : IStripeSubscriptionScheduleAdapter
{
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
