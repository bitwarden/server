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
    private const int _minimumThroughput = 3;

    private readonly IOrganizationIntegrationRepository _integrationRepository =
        Substitute.For<IOrganizationIntegrationRepository>();
    private readonly IFusionCache _cache = Substitute.For<IFusionCache>();

    private IntegrationCircuitBreaker BuildSut(int minimumThroughput = _minimumThroughput)
    {
        var globalSettings = new GlobalSettings();
        globalSettings.EventLogging.IntegrationCircuitBreakerMinimumThroughput = minimumThroughput;
        globalSettings.EventLogging.IntegrationCircuitBreakerFailureRatio = 1.0;

        _integrationRepository
            .DisableAsync(
                Arg.Any<Guid>(),
                Arg.Any<IntegrationType>(),
                Arg.Any<DateTime>(),
                Arg.Any<IntegrationFailureCategory>())
            .Returns(true);

        return new IntegrationCircuitBreaker(
            _integrationRepository,
            _cache,
            new ResiliencePipelineRegistry<IntegrationCircuitBreakerKey>(),
            globalSettings,
            new FakeTimeProvider(),
            NullLogger<IntegrationCircuitBreaker>.Instance);
    }

    private static IntegrationMessage BuildMessage(string? organizationId = null) => new()
    {
        IntegrationType = IntegrationType.Webhook,
        MessageId = "message-id",
        OrganizationId = organizationId ?? _organizationId.ToString(),
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
                previous.ContinueWith(_ => sut.RecordResultAsync(result.Message, result)).Unwrap());
    }

    private async Task AssertNotDisabledAsync()
    {
        await _integrationRepository.DidNotReceiveWithAnyArgs()
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

        await _integrationRepository.Received(1).DisableAsync(
            Arg.Is(_organizationId),
            Arg.Is(IntegrationType.Webhook),
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
    public async Task RecordResultAsync_SuccessBeforeMinimumThroughput_KeepsTheCircuitClosed()
    {
        var sut = BuildSut();
        var message = BuildMessage();

        await RecordAsync(sut, NonRetryableFailure(message), _minimumThroughput - 1);
        await sut.RecordResultAsync(message, IntegrationHandlerResult.Succeed(message));
        await RecordAsync(sut, NonRetryableFailure(message), _minimumThroughput - 1);

        await AssertNotDisabledAsync();
    }

    [Fact]
    public async Task RecordResultAsync_RetryableFailures_NeverDisable()
    {
        var sut = BuildSut();
        var message = BuildMessage();

        await RecordAsync(sut, RetryableFailure(message), _minimumThroughput * 2);

        await AssertNotDisabledAsync();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task RecordResultAsync_MinimumThroughputNotPositive_NeverDisables(int minimumThroughput)
    {
        var sut = BuildSut(minimumThroughput);
        var message = BuildMessage();

        await RecordAsync(sut, NonRetryableFailure(message), 25);

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
        _integrationRepository
            .DisableAsync(
                Arg.Any<Guid>(),
                Arg.Any<IntegrationType>(),
                Arg.Any<DateTime>(),
                Arg.Any<IntegrationFailureCategory>())
            .Returns(false);

        await RecordAsync(sut, NonRetryableFailure(message), _minimumThroughput);

        await _cache.DidNotReceive().RemoveByTagAsync(
            Arg.Any<string>(),
            Arg.Any<FusionCacheEntryOptions>(),
            Arg.Any<CancellationToken>());
    }
}
