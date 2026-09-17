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

    private int _invalidSettingsLogged;

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

        // Polly retains the last handled outcome for as long as the circuit stays open, so only the fields the
        // pipeline reads are replayed. The message itself carries decrypted third-party credentials and must not
        // outlive the handler that produced it
        var outcome = new IntegrationOutcome(
            Success: result.Success,
            Retryable: result.Retryable,
            Category: result.Category,
            IntegrationType: message.IntegrationType);

        var key = new IntegrationCircuitBreakerKey(organizationId, configurationId);
        var pipeline = pipelineRegistry.GetOrAddPipeline<IntegrationOutcome>(
            key,
            (builder, context) => Build(builder, context.PipelineKey, settings));

        var context = ResilienceContextPool.Shared.Get();
        try
        {
            // Outcomes are replayed into the pipeline rather than wrapping the send, because the key is only
            // known once the message has been deserialized by the handler
            await pipeline.ExecuteOutcomeAsync(
                (_, _) => new ValueTask<Outcome<IntegrationOutcome>>(Outcome.FromResult(outcome)),
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
    private static bool CountsTowardBreaking(IntegrationOutcome outcome) =>
        !outcome.Success && !outcome.Retryable;

    // An out-of-range setting disables the breaker instead of throwing out of the pipeline factory on every
    // message, so it is logged once to keep a misconfigured deployment distinguishable from an unconfigured one
    private bool IsConfigured(GlobalSettings.EventLoggingSettings settings)
    {
        if (settings.IntegrationCircuitBreakerMinimumThroughput <= 0)
        {
            return false;
        }

        if (IsWithinSupportedRanges(settings))
        {
            return true;
        }

        if (Interlocked.Exchange(ref _invalidSettingsLogged, 1) == 0)
        {
            logger.LogWarning(
                "Integration circuit breaker settings are outside the supported ranges, so it is disabled. " +
                "MinimumThroughput: {Throughput}, FailureRatio: {Ratio}, SamplingDuration: {Duration}",
                settings.IntegrationCircuitBreakerMinimumThroughput,
                settings.IntegrationCircuitBreakerFailureRatio,
                settings.IntegrationCircuitBreakerSamplingDuration);
        }

        return false;
    }

    // Polly's own ranges, except that a zero ratio is rejected here because it sets no threshold
    private static bool IsWithinSupportedRanges(GlobalSettings.EventLoggingSettings settings) =>
        settings.IntegrationCircuitBreakerMinimumThroughput >= MinimumSupportedThroughput &&
        settings.IntegrationCircuitBreakerFailureRatio > 0 &&
        settings.IntegrationCircuitBreakerFailureRatio <= 1 &&
        settings.IntegrationCircuitBreakerSamplingDuration >= MinimumSupportedDuration &&
        settings.IntegrationCircuitBreakerSamplingDuration <= MaximumSupportedDuration;

    private ResiliencePipelineBuilder<IntegrationOutcome> Build(
        ResiliencePipelineBuilder<IntegrationOutcome> builder,
        IntegrationCircuitBreakerKey key,
        GlobalSettings.EventLoggingSettings settings)
    {
        // Wired so the sampling window advances on the injected clock and stays testable
        builder.TimeProvider = timeProvider;

        return builder.AddCircuitBreaker(new CircuitBreakerStrategyOptions<IntegrationOutcome>
        {
            ShouldHandle = new PredicateBuilder<IntegrationOutcome>().HandleResult(CountsTowardBreaking),
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

    private async ValueTask TryDisableAsync(IntegrationCircuitBreakerKey key, IntegrationOutcome? outcome)
    {
        try
        {
            await DisableAsync(key, outcome);
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

    private async ValueTask DisableAsync(IntegrationCircuitBreakerKey key, IntegrationOutcome? outcome)
    {
        if (outcome is not { } failure || failure.Category is not { } failureCategory)
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
                integrationType: failure.IntegrationType));

        logger.LogWarning(
            "Integration configuration disabled by the circuit breaker. OrganizationId: {OrgId}, " +
            "IntegrationType: {IntegrationType}, ConfigurationId: {ConfigurationId}, FailureCategory: {Category}",
            key.OrganizationId,
            failure.IntegrationType,
            key.ConfigurationId,
            failureCategory);
    }
}

public readonly record struct IntegrationCircuitBreakerKey(Guid OrganizationId, Guid ConfigurationId);

/// <summary>
/// The only fields the circuit breaker's pipeline reads from a handler result. Every member must stay a value type:
/// Polly holds the last handled outcome for the life of an open circuit, and a disabled configuration stops
/// producing outcomes, so anything reachable from here is pinned until the process restarts.
/// </summary>
internal readonly record struct IntegrationOutcome(
    bool Success,
    bool Retryable,
    IntegrationFailureCategory? Category,
    IntegrationType IntegrationType);
