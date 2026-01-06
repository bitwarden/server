using System.Globalization;
using System.Text.Json;
using Bit.Billing.Services;
using Bit.Core;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Pricing;
using Bit.Core.Jobs;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Quartz;
using Stripe;
using OrganizationPlan = Bit.Core.Models.StaticStore.Plan;
using PremiumPlan = Bit.Core.Billing.Pricing.Premium.Plan;

namespace Bit.Billing.Jobs;

public class ReconcileAdditionalStorageJob(
    IStripeFacade stripeFacade,
    ILogger<ReconcileAdditionalStorageJob> logger,
    IFeatureService featureService,
    IUserRepository userRepository,
    IOrganizationRepository organizationRepository,
    IPricingClient pricingClient) : BaseJob(logger)
{
    private const string _storageGbMonthlyPriceId = "storage-gb-monthly";
    private const string _storageGbAnnuallyPriceId = "storage-gb-annually";
    private const string _personalStorageGbAnnuallyPriceId = "personal-storage-gb-annually";
    private const int _storageGbToRemove = 4;
    private const short _includedStorageGb = 5;

    public enum SubscriptionPlanTier
    {
        Personal,
        Organization,
        Unknown
    }

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
        var subscriptionsUpdatedInStripe = 0;
        var subscriptionsUpdatedInDatabase = 0;
        var subscriptionsWithErrors = 0;
        var databaseUpdatesFailed = 0;
        var failures = new List<string>();

        logger.LogInformation("Starting ReconcileAdditionalStorageJob (live mode: {LiveMode})", liveMode);

        // Load plans for subscription type determination
        List<PremiumPlan> personalPremiumPlans;
        List<OrganizationPlan> organizationPlans;
        try
        {
            personalPremiumPlans = await pricingClient.ListPremiumPlans();
            organizationPlans = await pricingClient.ListPlans();

            logger.LogInformation(
                "Loaded {PremiumCount} personal/premium plans and {OrgCount} organization plans from pricing client",
                personalPremiumPlans.Count,
                organizationPlans.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load pricing plans from pricing client. Cannot proceed with job execution.");
            return;
        }

        var priceIds = new[] { _storageGbMonthlyPriceId, _storageGbAnnuallyPriceId, _personalStorageGbAnnuallyPriceId };
        var stripeStatusesToProcess = new[] { StripeConstants.SubscriptionStatus.Active, StripeConstants.SubscriptionStatus.Trialing, StripeConstants.SubscriptionStatus.PastDue };

        foreach (var priceId in priceIds)
        {
            var options = new SubscriptionListOptions { Limit = 100, Price = priceId };

            await foreach (var subscription in stripeFacade.ListSubscriptionsAutoPagingAsync(options))
            {
                if (context.CancellationToken.IsCancellationRequested)
                {
                    logger.LogWarning(
                        "Job cancelled!! Exiting. Progress at time of cancellation: Subscriptions found: {SubscriptionsFound}, " +
                        "Updated: {SubscriptionsUpdated}, Errors: {SubscriptionsWithErrors}{Failures}",
                        subscriptionsFound,
                        liveMode
                            ? subscriptionsUpdatedInStripe
                            : $"(In live mode, would have updated) {subscriptionsUpdatedInStripe}",
                        subscriptionsWithErrors,
                        failures.Count > 0
                            ? $", Failures: {Environment.NewLine}{string.Join(Environment.NewLine, failures)}"
                            : string.Empty
                    );
                    return;
                }

                if (subscription == null)
                {
                    continue;
                }

                if (!stripeStatusesToProcess.Contains(subscription.Status))
                {
                    logger.LogInformation("Skipping subscription with unsupported status: {SubscriptionId} - {Status}", subscription.Id, subscription.Status);
                    continue;
                }

                logger.LogInformation("Processing subscription: {SubscriptionId}", subscription.Id);
                subscriptionsFound++;

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

                var updateOptions = BuildSubscriptionUpdateOptions(subscription, priceId);

                if (updateOptions == null)
                {
                    logger.LogInformation("Skipping subscription {SubscriptionId} - no updates needed", subscription.Id);
                    continue;
                }

                subscriptionsUpdatedInStripe++;

                // Now, prepare the database update so we can log details out if not in live mode
                var subscriptionPlanTier = DetermineSubscriptionPlanTier(subscription, personalPremiumPlans, organizationPlans);
                // Calculate new MaxStorageGb
                var currentStorageQuantity = GetCurrentStorageQuantityFromSubscription(subscription, priceId);
                var newMaxStorageGb = CalculateNewMaxStorageGb(currentStorageQuantity, updateOptions);

                if (!liveMode)
                {
                    logger.LogInformation(
                        "Not live mode (dry-run): Would have updated subscription {SubscriptionId} with item changes: {NewLine}{UpdateOptions}" +
                        "{NewLine2}And would have updated database record tier: {Tier} to new MaxStorageGb: {MaxStorageGb}",
                        subscription.Id,
                        Environment.NewLine,
                        JsonSerializer.Serialize(updateOptions),
                        Environment.NewLine,
                        subscriptionPlanTier,
                        newMaxStorageGb);
                    continue;
                }

                // Live mode enabled - continue with updates to stripe and database
                try
                {
                    await stripeFacade.UpdateSubscription(subscription.Id, updateOptions);
                    logger.LogInformation("Successfully updated Stripe subscription: {SubscriptionId}", subscription.Id);

                    logger.LogInformation(
                        "Updating MaxStorageGb in database for subscription {SubscriptionId} ({Type}): New MaxStorageGb: {MaxStorage}",
                        subscription.Id,
                        subscriptionPlanTier,
                        newMaxStorageGb);

                    var dbUpdateSuccess = await UpdateDatabaseMaxStorageAsync(
                        subscriptionPlanTier,
                        subscription.Id,
                        newMaxStorageGb);

                    if (!dbUpdateSuccess)
                    {
                        databaseUpdatesFailed++;
                        failures.Add($"Subscription {subscription.Id}: Database update failed");
                    }
                    else
                    {
                        subscriptionsUpdatedInDatabase++;
                    }
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
            "ReconcileAdditionalStorageJob FINISHED. Subscriptions found: {SubscriptionsFound}, " +
            "Stripe updates: {StripeUpdates}, Database updates: {DatabaseUpdates} succeeded / {DatabaseFailed} failed, " +
            "Errors: {SubscriptionsWithErrors}{Failures}",
            subscriptionsFound,
            liveMode
                ? subscriptionsUpdatedInStripe
                : $"(In live mode, would have updated) {subscriptionsUpdatedInStripe}",
            subscriptionsUpdatedInDatabase,
            databaseUpdatesFailed,
            subscriptionsWithErrors,
            failures.Count > 0
                ? $", Failures: {Environment.NewLine}{string.Join(Environment.NewLine, failures)}"
                : string.Empty
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

        var updateOptions = new SubscriptionUpdateOptions { ProrationBehavior = StripeConstants.ProrationBehavior.CreateProrations, Metadata = new Dictionary<string, string> { [StripeConstants.MetadataKeys.StorageReconciled2025] = DateTime.UtcNow.ToString("o") }, Items = [] };

        var hasUpdates = false;

        foreach (var item in subscription.Items.Data.Where(item => item?.Price?.Id == targetPriceId))
        {
            hasUpdates = true;
            var currentQuantity = item.Quantity;

            if (currentQuantity > _storageGbToRemove)
            {
                var newQuantity = currentQuantity - _storageGbToRemove;
                logger.LogInformation(
                    "Subscription {SubscriptionId}: reducing quantity from {CurrentQuantity} to {NewQuantity} for price {PriceId}",
                    subscription.Id,
                    currentQuantity,
                    newQuantity,
                    item.Price.Id);

                updateOptions.Items.Add(new SubscriptionItemOptions { Id = item.Id, Quantity = newQuantity });
            }
            else
            {
                logger.LogInformation("Subscription {SubscriptionId}: deleting storage item with quantity {CurrentQuantity} for price {PriceId}",
                    subscription.Id,
                    currentQuantity,
                    item.Price.Id);

                updateOptions.Items.Add(new SubscriptionItemOptions { Id = item.Id, Deleted = true });
            }
        }

        return hasUpdates ? updateOptions : null;
    }

    public SubscriptionPlanTier DetermineSubscriptionPlanTier(
        Subscription subscription,
        List<PremiumPlan> personalPremiumPlans,
        List<OrganizationPlan> organizationPlans)
    {
        if (subscription.Items?.Data == null)
        {
            return SubscriptionPlanTier.Unknown;
        }

        foreach (var item in subscription.Items.Data)
        {
            if (item?.Price?.Id == null)
            {
                continue;
            }

            // eagerly match the first id found to determine if personal or org
            if (personalPremiumPlans.Any(p => p.Seat.StripePriceId == item.Price.Id)) return SubscriptionPlanTier.Personal;

            if (organizationPlans.Any(p =>
                    p.PasswordManager.StripeSeatPlanId == item.Price.Id ||
                    p.PasswordManager.StripePlanId == item.Price.Id))
                return SubscriptionPlanTier.Organization;
        }

        return SubscriptionPlanTier.Unknown;
    }

    public long GetCurrentStorageQuantityFromSubscription(
        Subscription subscription,
        string storagePriceId)
    {
        return subscription.Items?.Data?.FirstOrDefault(item => item?.Price?.Id == storagePriceId)?.Quantity ?? 0;
    }

    public short CalculateNewMaxStorageGb(
        long currentQuantity,
        SubscriptionUpdateOptions? updateOptions)
    {
        if (updateOptions?.Items == null)
        {
            return (short)currentQuantity;
        }

        // If the update marks item as deleted, new quantity is whatever the base storage gb
        if (updateOptions.Items.Any(i => i.Deleted == true))
        {
            return _includedStorageGb;
        }

        // If the update has a new quantity, use it to calculate the new max
        var updatedItem = updateOptions.Items.FirstOrDefault(i => i.Quantity.HasValue);
        if (updatedItem?.Quantity != null)
        {
            return (short)(_includedStorageGb + updatedItem.Quantity.Value);
        }

        // Otherwise, no change
        return (short)currentQuantity;
    }

    public async Task<bool> UpdateDatabaseMaxStorageAsync(
        SubscriptionPlanTier subscriptionPlanTier,
        string subscriptionId,
        short newMaxStorageGb)
    {
        try
        {
            switch (subscriptionPlanTier)
            {
                case SubscriptionPlanTier.Personal:
                    {
                        var user = await userRepository.GetByGatewaySubscriptionIdAsync(subscriptionId);
                        if (user == null)
                        {
                            logger.LogError(
                                "User not found for subscription {SubscriptionId}. Database not updated.",
                                subscriptionId);
                            return false;
                        }

                        user.MaxStorageGb = newMaxStorageGb;
                        await userRepository.ReplaceAsync(user);

                        logger.LogInformation(
                            "Successfully updated User {UserId} MaxStorageGb to {MaxStorageGb} for subscription {SubscriptionId}",
                            user.Id,
                            newMaxStorageGb,
                            subscriptionId);
                        return true;
                    }
                case SubscriptionPlanTier.Organization:
                    {
                        var organization = await organizationRepository.GetByGatewaySubscriptionIdAsync(subscriptionId);
                        if (organization == null)
                        {
                            logger.LogWarning(
                                "Organization not found for subscription {SubscriptionId}. Database not updated.",
                                subscriptionId);
                            return false;
                        }

                        organization.MaxStorageGb = newMaxStorageGb;
                        await organizationRepository.ReplaceAsync(organization);

                        logger.LogInformation(
                            "Successfully updated Organization {OrganizationId} MaxStorageGb to {MaxStorageGb} for subscription {SubscriptionId}",
                            organization.Id,
                            newMaxStorageGb,
                            subscriptionId);
                        return true;
                    }
                case SubscriptionPlanTier.Unknown:
                default:
                    return false;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failed to update database MaxStorageGb for subscription {SubscriptionId} (Plan Tier: {SubscriptionType})",
                subscriptionId,
                subscriptionPlanTier);
            return false;
        }
    }

    public static ITrigger GetTrigger()
    {
        return TriggerBuilder.Create()
            .WithIdentity("EveryMorningTrigger")
            .StartNow()
            .WithCronSchedule("0 0 16 * * ?") // 10am CST daily; the pods execute in UTC time
            .Build();
    }
}
