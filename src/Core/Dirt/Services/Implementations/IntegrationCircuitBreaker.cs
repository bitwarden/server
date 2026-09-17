using Bit.Core.Dirt.Enums;
using Bit.Core.Dirt.Models.Data.EventIntegrations;
using Bit.Core.Dirt.Repositories;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using Polly.Registry;
using ZiggyCreatures.Caching.Fusion;

namespace Bit.Core.Dirt.Services.Implementations;

public class IntegrationCircuitBreaker(
    IOrganizationIntegrationRepository integrationRepository,
    IOrganizationIntegrationConfigurationRepository configurationRepository,
    [FromKeyedServices(EventIntegrationsCacheConstants.CacheName)]
    IFusionCache cache,
    ResiliencePipelineRegistry<IntegrationCircuitBreakerKey> pipelineRegistry,
    GlobalSettings globalSettings,
    TimeProvider timeProvider,
    ILogger<IntegrationCircuitBreaker> logger)
    : IIntegrationCircuitBreaker
{
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
        var settings = globalSettings.EventLogging;
        if (settings.IntegrationCircuitBreakerMinimumThroughput <= 0 ||
            !Guid.TryParse(message.OrganizationId, out var organizationId))
        {
            return;
        }

        // Prefer configuration scope, so one broken configuration cannot disable an integration whose other
        // configurations still deliver. Messages published before this field existed fall back to the integration.
        var key = new IntegrationCircuitBreakerKey(
            organizationId,
            message.IntegrationType,
            message.ConfigurationId);
        var pipeline = pipelineRegistry.GetOrAddPipeline<IntegrationHandlerResult>(
            key,
            (builder, context) => Build(builder, context.PipelineKey, settings));

        var context = ResilienceContextPool.Shared.Get();
        try
        {
            // Outcomes are replayed into the pipeline rather than wrapping the send, because the key is only
            // known once the message has been deserialized by the handler
            await pipeline.ExecuteOutcomeAsync(
                (_, _) => new ValueTask<Outcome<IntegrationHandlerResult>>(Outcome.FromResult(result)),
                context,
                state: 0);
        }
        finally
        {
            ResilienceContextPool.Shared.Return(context);
        }
    }

    private ResiliencePipelineBuilder<IntegrationHandlerResult> Build(
        ResiliencePipelineBuilder<IntegrationHandlerResult> builder,
        IntegrationCircuitBreakerKey key,
        GlobalSettings.EventLoggingSettings settings)
    {
        return builder.AddCircuitBreaker(new CircuitBreakerStrategyOptions<IntegrationHandlerResult>
        {
            // Only failures that will not resolve without someone changing the configuration count
            ShouldHandle = new PredicateBuilder<IntegrationHandlerResult>()
                .HandleResult(result => !result.Success && !result.Retryable),
            FailureRatio = settings.IntegrationCircuitBreakerFailureRatio,
            MinimumThroughput = settings.IntegrationCircuitBreakerMinimumThroughput,
            SamplingDuration = settings.IntegrationCircuitBreakerSamplingDuration,
            BreakDuration = settings.IntegrationCircuitBreakerSamplingDuration,

            // The disabled row, not this circuit, is what holds the integration off until an admin reconfigures
            // it. Letting the circuit recover on its own costs nothing, because disabling is idempotent.
            OnOpened = args => DisableAsync(key, args.Outcome.Result)
        });
    }

    private async ValueTask DisableAsync(IntegrationCircuitBreakerKey key, IntegrationHandlerResult? result)
    {
        if (result?.Category is not IntegrationFailureCategory failureCategory)
        {
            return;
        }

        var disabledDate = timeProvider.GetUtcNow().UtcDateTime;
        var disabled = key.ConfigurationId is Guid configurationId
            ? await configurationRepository.DisableAsync(
                id: configurationId,
                disabledDate: disabledDate,
                disabledReason: failureCategory)
            : await integrationRepository.DisableAsync(
                organizationId: key.OrganizationId,
                integrationType: key.IntegrationType,
                disabledDate: disabledDate,
                disabledReason: failureCategory);

        if (!disabled)
        {
            return;
        }

        await cache.RemoveByTagAsync(
            EventIntegrationsCacheConstants.BuildCacheTagForOrganizationIntegration(
                organizationId: key.OrganizationId,
                integrationType: key.IntegrationType));

        logger.LogWarning(
            "Integration disabled by the circuit breaker. OrganizationId: {OrgId}, " +
            "IntegrationType: {IntegrationType}, ConfigurationId: {ConfigurationId}, FailureCategory: {Category}",
            key.OrganizationId,
            key.IntegrationType,
            key.ConfigurationId,
            failureCategory);
    }
}

public readonly record struct IntegrationCircuitBreakerKey(
    Guid OrganizationId,
    IntegrationType IntegrationType,
    Guid? ConfigurationId);
