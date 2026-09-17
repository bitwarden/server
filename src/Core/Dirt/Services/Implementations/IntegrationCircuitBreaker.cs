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
    // Polly requires at least two sampled outcomes before it will evaluate a circuit
    internal const int MinimumSupportedThroughput = 2;

    // Polly's accepted range for both SamplingDuration and BreakDuration
    internal static readonly TimeSpan MinimumSupportedDuration = TimeSpan.FromMilliseconds(500);
    internal static readonly TimeSpan MaximumSupportedDuration = TimeSpan.FromDays(1);

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
        if (!IsConfigured(settings))
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

    // A rate-limited or unavailable service recovers on its own and should not cost an organization its
    // integration, while an authentication, configuration, or permanent failure will not recover on its own
    private static bool CountsTowardBreaking(IntegrationHandlerResult result) =>
        !result.Success && !result.Retryable;

    // Every value Polly range-checks is validated here, so an out-of-range setting disables the breaker instead of
    // throwing out of the pipeline factory on every message
    private static bool IsConfigured(GlobalSettings.EventLoggingSettings settings) =>
        settings.IntegrationCircuitBreakerMinimumThroughput >= MinimumSupportedThroughput &&
        settings.IntegrationCircuitBreakerFailureRatio > 0 &&
        settings.IntegrationCircuitBreakerFailureRatio <= 1 &&
        settings.IntegrationCircuitBreakerSamplingDuration >= MinimumSupportedDuration &&
        settings.IntegrationCircuitBreakerSamplingDuration <= MaximumSupportedDuration;

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

            // A failure here leaves the configuration enabled and the circuit already open, and Polly fires this
            // once per open transition, so the write is retried by the next transition after the break rather than
            // lost silently
            OnOpened = args => TryDisableAsync(key, args.Outcome.Result)
        });
    }

    private async ValueTask TryDisableAsync(IntegrationCircuitBreakerKey key, IntegrationHandlerResult? result)
    {
        try
        {
            await DisableAsync(key, result);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to disable a configuration the circuit breaker tripped on. OrganizationId: {OrgId}, " +
                "ConfigurationId: {ConfigurationId}",
                key.OrganizationId,
                key.ConfigurationId);
        }
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
