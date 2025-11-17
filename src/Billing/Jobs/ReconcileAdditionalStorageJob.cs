using System.Globalization;
using Bit.Billing.Services;
using Bit.Core;
using Bit.Core.Billing.Constants;
using Bit.Core.Jobs;
using Bit.Core.Services;
using Quartz;
using Stripe;
using System.Text.Json;

namespace Bit.Billing.Jobs;

public class ReconcileAdditionalStorageJob(
    IStripeFacade stripeFacade,
    ILogger<ReconcileAdditionalStorageJob> logger,
    IFeatureService featureService) : BaseJob(logger)
{
    private const string _storageGbMonthlyPriceId = "storage-gb-monthly";
    private const string _storageGbAnnuallyPriceId = "storage-gb-annually";
    private const string _personalStorageGbAnnuallyPriceId = "personal-storage-gb-annually";

    protected override async Task ExecuteJobAsync(IJobExecutionContext context)
    {
        if (!featureService.IsEnabled(FeatureFlagKeys.PM28265_EnableReconcileAdditionalStorageJob))
        {
            logger.LogInformation("Skipping ReconcileAdditionalStorageJob, feature flag off.");
            return;
        }

        var liveMode = featureService.IsEnabled(FeatureFlagKeys.PM28265_ReconcileAdditionalStorageJobEnableLiveMode);

        // Execution tracking
        var subscriptionsFound = 0;
        var subscriptionsUpdated = 0;
        var subscriptionsWithErrors = 0;
        var failures = new List<string>();

        logger.LogInformation("Starting ReconcileAdditionalStorageJob (live mode: {LiveMode})", liveMode);

        var priceIds = new[] { _storageGbMonthlyPriceId, _storageGbAnnuallyPriceId, _personalStorageGbAnnuallyPriceId };

        foreach (var priceId in priceIds)
        {
            var options = new SubscriptionListOptions { Limit = 100, Status = "active", Price = priceId };

            await foreach (var subscription in stripeFacade.ListSubscriptionsAutoPagingAsync(options))
            {
                if (context.CancellationToken.IsCancellationRequested)
                {
                    logger.LogWarning(
                        "Job cancelled!! Exiting. Progress at time of cancellation: Subscriptions found: {SubscriptionsFound}, " +
                        "Updated: {SubscriptionsUpdated}, Errors: {SubscriptionsWithErrors}{Failures}",
                        subscriptionsFound,
                        liveMode
                            ? subscriptionsUpdated
                            : $"(In live mode, would have updated) {subscriptionsUpdated}",
                        subscriptionsWithErrors,
                        failures.Count > 0
                            ? $", Failures: {Environment.NewLine}{string.Join(Environment.NewLine, failures)}"
                            :  string.Empty
                        );
                    return;
                }

                if (subscription == null)
                {
                    continue;
                }

                if (subscription.Metadata?.TryGetValue(StripeConstants.MetadataKeys.StorageReconciled2025, out var dateString) == true)
                {
                    if (DateTime.TryParse(dateString, null, DateTimeStyles.RoundtripKind, out var dateProcessed))
                    {
                        logger.LogInformation("Skipping subscription {SubscriptionId} - already processed on {Date}",
                            subscription.Id,
                            dateProcessed.ToString("f"));
                        continue;
                    }
                }

                logger.LogInformation("Processing subscription: {SubscriptionId}", subscription.Id);
                subscriptionsFound++;

                var updateOptions = BuildSubscriptionUpdateOptions(subscription, priceId);

                if (updateOptions == null)
                {
                    logger.LogInformation("Skipping subscription {SubscriptionId} - no updates needed", subscription.Id);
                    continue;
                }

                subscriptionsUpdated++;

                if (!liveMode)
                {
                    logger.LogInformation(
                        "Not live mode (dry-run): Would have updated subscription {SubscriptionId} with item changes: {NewLine}{UpdateOptions}",
                        subscription.Id,
                        Environment.NewLine,
                        JsonSerializer.Serialize(updateOptions));
                    continue;
                }

                try
                {
                    await stripeFacade.UpdateSubscription(subscription.Id, updateOptions);
                    logger.LogInformation("Successfully updated subscription: {SubscriptionId}", subscription.Id);
                }
                catch (Exception ex)
                {
                    subscriptionsWithErrors++;
                    failures.Add($"Subscription {subscription.Id}: {ex.Message}");
                    logger.LogError(ex, "Failed to update subscription {SubscriptionId}: {ErrorMessage}",
                        subscription.Id, ex.Message);
                }
            }
        }

        logger.LogInformation(
            "ReconcileAdditionalStorageJob completed. Subscriptions found: {SubscriptionsFound}, " +
            "Updated: {SubscriptionsUpdated}, Errors: {SubscriptionsWithErrors}{Failures}",
            subscriptionsFound,
            liveMode
                ? subscriptionsUpdated
                : $"(In live mode, would have updated) {subscriptionsUpdated}",
            subscriptionsWithErrors,
            failures.Count > 0
                ? $", Failures: {Environment.NewLine}{string.Join(Environment.NewLine, failures)}"
                :  string.Empty
            );
    }

    private SubscriptionUpdateOptions? BuildSubscriptionUpdateOptions(
        Subscription subscription,
        string targetPriceId)
    {
        if (subscription.Items?.Data == null)
        {
            return null;
        }

        var updateOptions = new SubscriptionUpdateOptions
        {
            ProrationBehavior = "always_invoice",
            Metadata = new Dictionary<string, string>
            {
                [StripeConstants.MetadataKeys.StorageReconciled2025] = DateTime.UtcNow.ToString("o")
            },
            Items = []
        };

        var hasUpdates = false;

        foreach (var item in subscription.Items.Data.Where(item => item?.Price?.Id == targetPriceId))
        {
            hasUpdates = true;
            var currentQuantity = item.Quantity;

            if (currentQuantity > 4)
            {
                var newQuantity = currentQuantity - 4;
                logger.LogInformation(
                    "Subscription {SubscriptionId}: reducing quantity from {CurrentQuantity} to {NewQuantity} for price {PriceId}",
                    subscription.Id,
                    currentQuantity,
                    newQuantity,
                    item.Price.Id);

                updateOptions.Items.Add(new SubscriptionItemOptions
                {
                    Id = item.Id,
                    Quantity = newQuantity
                });
            }
            else
            {
                logger.LogInformation("Subscription {SubscriptionId}: deleting storage item with quantity {CurrentQuantity} for price {PriceId}",
                    subscription.Id,
                    currentQuantity,
                    item.Price.Id);

                updateOptions.Items.Add(new SubscriptionItemOptions
                {
                    Id = item.Id,
                    Deleted = true
                });
            }
        }

        return hasUpdates ? updateOptions : null;
    }

    public static ITrigger GetTrigger()
    {
        return TriggerBuilder.Create()
            .WithIdentity("EveryMorningTrigger")
            .StartNow()
            .WithCronSchedule("0 0 16 * * ?") // 10am CST daily, assuming the pods execute in UTC time
            .Build();
    }

    public static async Task RunJobNowAsync(ISchedulerFactory schedulerFactory)
    {
        var scheduler = await schedulerFactory.GetScheduler();

        var jobKey = new JobKey(nameof(ReconcileAdditionalStorageJob));

        var currentlyExecuting = await scheduler.GetCurrentlyExecutingJobs();
        if (currentlyExecuting.Any(j => j.JobDetail.Key.Equals(jobKey)))
        {
            throw new InvalidOperationException("Job is already running");
        }

        var job = JobBuilder.Create<ReconcileAdditionalStorageJob>()
            .WithIdentity(jobKey)
            .Build();

        var trigger = TriggerBuilder.Create()
            .WithIdentity("ReconcileAdditionalStorageJob.RunNow")
            .StartNow()
            .Build();

        await scheduler.ScheduleJob(job, trigger);
    }
}
