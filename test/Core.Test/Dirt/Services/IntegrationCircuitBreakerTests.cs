#nullable enable

using Bit.Core.Dirt.Enums;
using Bit.Core.Dirt.Models.Data.EventIntegrations;
using Bit.Core.Dirt.Repositories;
using Bit.Core.Dirt.Services.Implementations;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Polly.Registry;
using Xunit;
using ZiggyCreatures.Caching.Fusion;

namespace Bit.Core.Test.Dirt.Services;

public class IntegrationCircuitBreakerTests
{
    private static readonly Guid _organizationId = Guid.Parse("6a3b0e4e-3f2e-4a1b-9b6e-2f5a7c1d8e90");
    private static readonly Guid _configurationId = Guid.Parse("3f1c9d2b-7e8a-4c5d-9a1b-6e2f4c8d0a37");
    private const int _minimumThroughput = 3;

    private readonly IOrganizationIntegrationConfigurationRepository _configurationRepository =
        Substitute.For<IOrganizationIntegrationConfigurationRepository>();
    private readonly IFusionCache _cache = Substitute.For<IFusionCache>();
    private readonly FakeTimeProvider _timeProvider = new();

    private readonly GlobalSettings _globalSettings = new();
    private GlobalSettings.EventLoggingSettings _settings => _globalSettings.EventLogging;

    private IntegrationCircuitBreaker BuildSut(int minimumThroughput = _minimumThroughput)
    {
        var globalSettings = _globalSettings;
        globalSettings.EventLogging.IntegrationCircuitBreakerMinimumThroughput = minimumThroughput;

        _configurationRepository
            .DisableAsync(
                Arg.Any<Guid>(),
                Arg.Any<Guid>(),
                Arg.Any<DateTime>(),
                Arg.Any<IntegrationFailureCategory>())
            .Returns(true);

        return new IntegrationCircuitBreaker(
            _configurationRepository,
            _cache,
            new ResiliencePipelineRegistry<IntegrationCircuitBreakerKey>(),
            globalSettings,
            _timeProvider,
            NullLogger<IntegrationCircuitBreaker>.Instance);
    }

    private static IntegrationMessage BuildMessage(
        string? organizationId = null,
        Guid? configurationId = null) => new()
        {
            IntegrationType = IntegrationType.Webhook,
            MessageId = "message-id",
            OrganizationId = organizationId ?? _organizationId.ToString(),
            ConfigurationId = configurationId ?? _configurationId,
            RenderedTemplate = "{}"
        };

    private static IntegrationHandlerResult NonRetryableFailure(IntegrationMessage message) =>
        IntegrationHandlerResult.Fail(message, IntegrationFailureCategory.AuthenticationFailed, "401");

    private static IntegrationHandlerResult RetryableFailure(IntegrationMessage message) =>
        IntegrationHandlerResult.Fail(message, IntegrationFailureCategory.ServiceUnavailable, "503");

    private Task RecordAsync(IntegrationCircuitBreaker sut, IntegrationHandlerResult result, int times)
    {
        return Enumerable.Range(0, times)
            .Aggregate(Task.CompletedTask, (previous, _) =>
                previous.ContinueWith(_ => sut.RecordResultAsync(result)).Unwrap());
    }

    private async Task AssertNotDisabledAsync()
    {
        await _configurationRepository.DidNotReceiveWithAnyArgs()
            .DisableAsync(default, default, default, default);
    }

    [Fact]
    public async Task RecordResultAsync_FailuresBelowMinimumThroughput_DoesNotDisable()
    {
        var sut = BuildSut();
        var message = BuildMessage();

        await RecordAsync(sut, NonRetryableFailure(message), _minimumThroughput - 1);

        await AssertNotDisabledAsync();
    }

    [Fact]
    public async Task RecordResultAsync_FailuresReachMinimumThroughput_DisablesWithTheFailureCategory()
    {
        var sut = BuildSut();
        var message = BuildMessage();

        await RecordAsync(sut, NonRetryableFailure(message), _minimumThroughput);

        await _configurationRepository.Received(1).DisableAsync(
            Arg.Is(_organizationId),
            Arg.Is(_configurationId),
            Arg.Any<DateTime>(),
            Arg.Is(IntegrationFailureCategory.AuthenticationFailed));
        await _cache.Received(1).RemoveByTagAsync(
            Arg.Is(EventIntegrationsCacheConstants.BuildCacheTagForOrganizationIntegration(
                _organizationId,
                IntegrationType.Webhook)),
            Arg.Any<FusionCacheEntryOptions>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordResultAsync_SuccessesBetweenFailures_DoNotResetTheCount()
    {
        // The circuit counts non-retryable failures inside the window and nothing else, so the threshold has to be
        // chosen for the volume it will see rather than relying on successes to dilute it
        var sut = BuildSut();
        var message = BuildMessage();

        await RecordAsync(sut, NonRetryableFailure(message), _minimumThroughput - 1);
        await sut.RecordResultAsync(IntegrationHandlerResult.Succeed(message));
        await sut.RecordResultAsync(NonRetryableFailure(message));

        await _configurationRepository.Received(1).DisableAsync(
            Arg.Is(_organizationId),
            Arg.Is(_configurationId),
            Arg.Any<DateTime>(),
            Arg.Is(IntegrationFailureCategory.AuthenticationFailed));
    }

    [Fact]
    public async Task RecordResultAsync_RetryableFailures_NeverDisable()
    {
        var sut = BuildSut();
        var message = BuildMessage();

        await RecordAsync(sut, RetryableFailure(message), _minimumThroughput * 2);

        await AssertNotDisabledAsync();
    }

    [Fact]
    public async Task RecordResultAsync_MessageWithoutOrganization_NeverDisables()
    {
        var sut = BuildSut();
        var message = BuildMessage(organizationId: null!);
        message.OrganizationId = null;

        await RecordAsync(sut, NonRetryableFailure(message), _minimumThroughput);

        await AssertNotDisabledAsync();
    }

    [Fact]
    public async Task RecordResultAsync_AlreadyDisabled_DoesNotInvalidateCache()
    {
        var sut = BuildSut();
        var message = BuildMessage();
        _configurationRepository
            .DisableAsync(
                Arg.Any<Guid>(),
                Arg.Any<Guid>(),
                Arg.Any<DateTime>(),
                Arg.Any<IntegrationFailureCategory>())
            .Returns(false);

        await RecordAsync(sut, NonRetryableFailure(message), _minimumThroughput);

        await _cache.DidNotReceive().RemoveByTagAsync(
            Arg.Any<string>(),
            Arg.Any<FusionCacheEntryOptions>(),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1)]
    public async Task RecordResultAsync_ThroughputBelowPollyMinimum_NeverDisables(int minimumThroughput)
    {
        // Polly rejects a minimum throughput under 2, so the guard has to stop short of handing it to the builder
        var sut = BuildSut(minimumThroughput);
        var message = BuildMessage();

        await RecordAsync(sut, NonRetryableFailure(message), 25);

        await AssertNotDisabledAsync();
    }

    [Fact]
    public async Task RecordResultAsync_MessageWithoutConfigurationId_NeverDisables()
    {
        var sut = BuildSut();
        var message = BuildMessage();
        message.ConfigurationId = null;

        await RecordAsync(sut, NonRetryableFailure(message), _minimumThroughput);

        await AssertNotDisabledAsync();
    }

    [Fact]
    public async Task RecordResultAsync_MessageWithEmptyConfigurationId_NeverDisables()
    {
        var sut = BuildSut();
        var message = BuildMessage(configurationId: Guid.Empty);

        await RecordAsync(sut, NonRetryableFailure(message), _minimumThroughput);

        await AssertNotDisabledAsync();
    }

    [Fact]
    public async Task RecordResultAsync_FailuresOutsideTheSamplingWindow_DoNotAccumulate()
    {
        var sut = BuildSut();
        var message = BuildMessage();
        var window = new GlobalSettings().EventLogging.IntegrationCircuitBreakerSamplingDuration;

        for (var i = 0; i < _minimumThroughput * 2; i++)
        {
            await sut.RecordResultAsync(NonRetryableFailure(message));
            _timeProvider.Advance(window + TimeSpan.FromMinutes(1));
        }

        await AssertNotDisabledAsync();
    }

    [Fact]
    public async Task RecordResultAsync_AfterATripAndRecovery_DoesNotReDisableOnASingleFailure()
    {
        var sut = BuildSut();
        var message = BuildMessage();

        // Trip it, then stand in for an admin fixing the integration by letting the disable succeed again
        await RecordAsync(sut, NonRetryableFailure(message), _minimumThroughput);
        _configurationRepository.ClearReceivedCalls();

        // The circuit is held open, so the next failure short-circuits instead of re-opening and re-writing
        _timeProvider.Advance(new GlobalSettings().EventLogging.IntegrationCircuitBreakerSamplingDuration
            + TimeSpan.FromMinutes(1));
        await sut.RecordResultAsync(NonRetryableFailure(message));

        await AssertNotDisabledAsync();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public async Task RecordResultAsync_SamplingDurationOutsidePollyRange_NeverDisables(int samplingDays)
    {
        var sut = BuildSut();
        var message = BuildMessage();
        _settings.IntegrationCircuitBreakerSamplingDuration = TimeSpan.FromDays(samplingDays);

        await RecordAsync(sut, NonRetryableFailure(message), _minimumThroughput * 2);

        await AssertNotDisabledAsync();
    }
}
