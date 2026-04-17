// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.AdminConsole.OrganizationFeatures.Organizations.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Quartz;

namespace Bit.Billing.Jobs;

public class ProviderOrganizationDisableJob(
    IProviderOrganizationRepository providerOrganizationRepository,
    IOrganizationDisableCommand organizationDisableCommand,
    ILogger<ProviderOrganizationDisableJob> logger)
    : IJob
{
    private const int MaxConcurrency = 5;
    private const int MaxTimeoutMinutes = 10;

    public async Task Execute(IJobExecutionContext context)
    {
        var providerId = new Guid(context.MergedJobDataMap.GetString("providerId") ?? string.Empty);
        var expirationDateString = context.MergedJobDataMap.GetString("expirationDate");
        DateTime? expirationDate = string.IsNullOrEmpty(expirationDateString)
            ? null
            : DateTime.Parse(expirationDateString);

        logger.LogInformation("Starting to disable organizations for provider {ProviderId}", providerId);

        var startTime = DateTime.UtcNow;
        var totalProcessed = 0;
        var totalErrors = 0;

        try
        {
            var providerOrganizations = await providerOrganizationRepository
                .GetManyDetailsByProviderAsync(providerId);

            if (providerOrganizations == null || !providerOrganizations.Any())
            {
                logger.LogInformation("No organizations found for provider {ProviderId}", providerId);
                return;
            }

            logger.LogInformation("Disabling {OrganizationCount} organizations for provider {ProviderId}",
                providerOrganizations.Count, providerId);

            var semaphore = new SemaphoreSlim(MaxConcurrency, MaxConcurrency);
            var tasks = providerOrganizations.Select(async po =>
            {
                if (DateTime.UtcNow.Subtract(startTime).TotalMinutes > MaxTimeoutMinutes)
                {
                    logger.LogWarning("Timeout reached while disabling organizations for provider {ProviderId}", providerId);
                    return false;
                }

                await semaphore.WaitAsync();
                try
                {
                    await organizationDisableCommand.DisableAsync(po.OrganizationId, expirationDate);
                    Interlocked.Increment(ref totalProcessed);
                    return true;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to disable organization {OrganizationId} for provider {ProviderId}",
                        po.OrganizationId, providerId);
                    Interlocked.Increment(ref totalErrors);
                    return false;
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);

            logger.LogInformation("Completed disabling organizations for provider {ProviderId}. Processed: {TotalProcessed}, Errors: {TotalErrors}",
                providerId, totalProcessed, totalErrors);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error disabling organizations for provider {ProviderId}. Processed: {TotalProcessed}, Errors: {TotalErrors}",
                providerId, totalProcessed, totalErrors);
            throw;
        }
    }
}
