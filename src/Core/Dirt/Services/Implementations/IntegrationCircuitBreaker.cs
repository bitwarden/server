using System.Collections.Concurrent;
using Bit.Core.Dirt.Enums;
using Bit.Core.Dirt.Models.Data.EventIntegrations;
using Bit.Core.Dirt.Repositories;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion;

namespace Bit.Core.Dirt.Services.Implementations;

public class IntegrationCircuitBreaker(
    IOrganizationIntegrationRepository integrationRepository,
    [FromKeyedServices(EventIntegrationsCacheConstants.CacheName)]
    IFusionCache cache,
    GlobalSettings globalSettings,
    TimeProvider timeProvider,
    ILogger<IntegrationCircuitBreaker> logger)
    : IIntegrationCircuitBreaker
{
    private readonly ConcurrentDictionary<(Guid OrganizationId, IntegrationType IntegrationType), int> _failureCounts
        = new();

    public async Task RecordResultAsync(IIntegrationMessage message, IntegrationHandlerResult result)
    {
        // The breaker must never change what the listener does with the message that triggered it
        try
        {
            await RecordAsync(message, result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to record an integration result for the circuit breaker.");
        }
    }

    private async Task RecordAsync(IIntegrationMessage message, IntegrationHandlerResult result)
    {
        var threshold = globalSettings.EventLogging.IntegrationCircuitBreakerThreshold;
        if (threshold <= 0 || !Guid.TryParse(message.OrganizationId, out var organizationId))
        {
            return;
        }

        var key = (organizationId, message.IntegrationType);

        if (result.Success)
        {
            _failureCounts.TryRemove(key, out _);
            return;
        }

        if (result.Retryable || result.Category is not IntegrationFailureCategory category)
        {
            return;
        }

        var failures = _failureCounts.AddOrUpdate(key, 1, (_, count) => count + 1);
        if (failures < threshold)
        {
            return;
        }

        // Nothing further to count for an integration that is about to stop receiving events
        _failureCounts.TryRemove(key, out _);

        await DisableAsync(organizationId, message.IntegrationType, category, threshold);
    }

    private async Task DisableAsync(
        Guid organizationId,
        IntegrationType integrationType,
        IntegrationFailureCategory failureCategory,
        int threshold)
    {
        var disabled = await integrationRepository.DisableAsync(
            organizationId: organizationId,
            integrationType: integrationType,
            disabledDate: timeProvider.GetUtcNow().UtcDateTime,
            disabledReason: failureCategory);

        if (!disabled)
        {
            return;
        }

        await cache.RemoveByTagAsync(
            EventIntegrationsCacheConstants.BuildCacheTagForOrganizationIntegration(
                organizationId: organizationId,
                integrationType: integrationType));

        logger.LogWarning(
            "Integration disabled after {Threshold} consecutive non-retryable failures. " +
            "OrganizationId: {OrgId}, IntegrationType: {IntegrationType}, FailureCategory: {Category}",
            threshold,
            organizationId,
            integrationType,
            failureCategory);
    }
}
