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
    IOrganizationIntegrationConfigurationRepository configurationRepository,
    [FromKeyedServices(EventIntegrationsCacheConstants.CacheName)]
    IFusionCache cache,
    ResiliencePipelineRegistry<IntegrationCircuitBreakerKey> pipelineRegistry,
    GlobalSettings globalSettings,
    TimeProvider timeProvider,
    ILogger<IntegrationCircuitBreaker> logger)
    : IIntegrationCircuitBreaker
{
    // Polly requires at least two attempts in the window before it will evaluate the failure ratio
    internal const int MinimumSupportedThroughput = 2;

    public async Task RecordResultAsync(IntegrationHandlerResult result)
    {
        // The breaker must never change what the listener does with the message that triggered it
        try
        {
            await RecordAsync(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to record an integration result for the circuit breaker.");
        }
    }

    private async Task RecordAsync(IntegrationHandlerResult result)
    {
        var settings = globalSettings.EventLogging;
        if (settings.IntegrationCircuitBreakerMinimumThroughput < MinimumSupportedThroughput)
        {
            return;
        }

        var message = result.Message;
        if (!Guid.TryParse(message.OrganizationId, out var organizationId) ||
            message.ConfigurationId is not Guid configurationId ||
            configurationId == Guid.Empty)
        {
            return;
        }

        var key = new IntegrationCircuitBreakerKey(organizationId, configurationId);

        // A counted failure creates the breaker; a success only feeds one that already exists. Successes still
        // dilute the failure ratio for a struggling configuration, while the registry stays proportional to the
        // configurations that are actually failing rather than to every one that has ever delivered.
        ResiliencePipeline<IntegrationHandlerResult> pipeline;
        if (CountsTowardBreaking(result))
        {
            pipeline = pipelineRegistry.GetOrAddPipeline<IntegrationHandlerResult>(
                key,
                (builder, context) => Build(builder, context.PipelineKey, settings));
        }
        else if (!pipelineRegistry.TryGetPipeline(key, out pipeline!))
        {
            return;
        }

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

    // A rate-limited or unavailable service recovers on its own and should not cost an organization its
    // integration, while an authentication, configuration, or permanent failure will not recover on its own
    private static bool CountsTowardBreaking(IntegrationHandlerResult result) =>
        !result.Success && !result.Retryable;

    private ResiliencePipelineBuilder<IntegrationHandlerResult> Build(
        ResiliencePipelineBuilder<IntegrationHandlerResult> builder,
        IntegrationCircuitBreakerKey key,
        GlobalSettings.EventLoggingSettings settings)
    {
        // Wired so the sampling window advances on the injected clock and stays testable
        builder.TimeProvider = timeProvider;

        return builder.AddCircuitBreaker(new CircuitBreakerStrategyOptions<IntegrationHandlerResult>
        {
            ShouldHandle = new PredicateBuilder<IntegrationHandlerResult>().HandleResult(CountsTowardBreaking),
            FailureRatio = settings.IntegrationCircuitBreakerFailureRatio,
            MinimumThroughput = settings.IntegrationCircuitBreakerMinimumThroughput,
            SamplingDuration = settings.IntegrationCircuitBreakerSamplingDuration,
            BreakDuration = settings.IntegrationCircuitBreakerSamplingDuration,

            // The disabled row, not this circuit, is what holds the configuration off until an admin reconfigures
            // it. Letting the circuit recover on its own costs nothing, because disabling is idempotent.
            OnOpened = args => DisableAsync(key, args.Outcome.Result)
        });
    }

    private async ValueTask DisableAsync(IntegrationCircuitBreakerKey key, IntegrationHandlerResult? result)
    {
        if (result?.Category is not { } failureCategory)
        {
            return;
        }

        var disabled = await configurationRepository.DisableAsync(
            organizationId: key.OrganizationId,
            id: key.ConfigurationId,
            disabledDate: timeProvider.GetUtcNow().UtcDateTime,
            disabledReason: failureCategory);

        if (!disabled)
        {
            return;
        }

        await cache.RemoveByTagAsync(
            EventIntegrationsCacheConstants.BuildCacheTagForOrganizationIntegration(
                organizationId: key.OrganizationId,
                integrationType: result.Message.IntegrationType));

        logger.LogWarning(
            "Integration configuration disabled by the circuit breaker. OrganizationId: {OrgId}, " +
            "IntegrationType: {IntegrationType}, ConfigurationId: {ConfigurationId}, FailureCategory: {Category}",
            key.OrganizationId,
            result.Message.IntegrationType,
            key.ConfigurationId,
            failureCategory);
    }
}

public readonly record struct IntegrationCircuitBreakerKey(Guid OrganizationId, Guid ConfigurationId);
