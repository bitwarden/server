using System.Globalization;
using Azure.Messaging.ServiceBus;
using Bit.Core.Dirt.Services;
using Bit.Core.Settings;
using Bit.EventsProcessor;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace EventsProcessor.Test;

public class DeadLetterCleanupHostedServiceTests
{
    private static readonly DateTimeOffset _cutoff = new(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);

    private static ServiceBusReceivedMessage BuildMessage(string messageId, DateTimeOffset enqueuedTime) =>
        ServiceBusModelFactory.ServiceBusReceivedMessage(
            messageId: messageId,
            enqueuedTime: enqueuedTime);

    private static ServiceBusReceiver BuildReceiver(params ServiceBusReceivedMessage[] messages)
    {
        var receiver = Substitute.For<ServiceBusReceiver>();
        receiver.ReceiveMessagesAsync(
                Arg.Any<int>(),
                Arg.Any<TimeSpan?>(),
                Arg.Any<CancellationToken>())
            .Returns(messages);
        return receiver;
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task ExecuteAsync_RetentionNotPositive_SweepsNothing(int retentionHours)
    {
        var serviceBusService = Substitute.For<IAzureServiceBusService>();
        var globalSettings = new GlobalSettings();
        globalSettings.EventLogging.AzureServiceBus.DeadLetterRetention = TimeSpan.FromHours(retentionHours);

        var sut = new DeadLetterCleanupHostedService(
            NullLogger<DeadLetterCleanupHostedService>.Instance,
            globalSettings,
            serviceBusService,
            TimeProvider.System);

        await sut.StartAsync(CancellationToken.None);
        await sut.StopAsync(CancellationToken.None);

        serviceBusService.DidNotReceiveWithAnyArgs().CreateDeadLetterReceiver(default!, default!);
    }

    [Fact]
    public void SweepInterval_ConfiguredPositive_UsesConfiguredValue()
    {
        var globalSettings = new GlobalSettings();
        globalSettings.EventLogging.AzureServiceBus.DeadLetterSweepInterval = TimeSpan.FromMinutes(15);

        Assert.Equal(
            TimeSpan.FromMinutes(15),
            DeadLetterCleanupHostedService.SweepInterval(
                globalSettings,
                NullLogger<DeadLetterCleanupHostedService>.Instance));
    }

    [Fact]
    public void SweepInterval_ConfiguredAtMaximum_UsesConfiguredValue()
    {
        var globalSettings = new GlobalSettings();
        globalSettings.EventLogging.AzureServiceBus.DeadLetterSweepInterval =
            DeadLetterCleanupHostedService.MaxSweepInterval;

        Assert.Equal(
            DeadLetterCleanupHostedService.MaxSweepInterval,
            DeadLetterCleanupHostedService.SweepInterval(
                globalSettings,
                NullLogger<DeadLetterCleanupHostedService>.Instance));
    }

    [Fact]
    public void MaxSweepInterval_SitsAtTheLongestDelayTaskDelayAccepts()
    {
        // Pre-canceled so no timer is left running; the delay is validated before the token is honored
        var canceled = new CancellationToken(canceled: true);

        Assert.True(Task.Delay(
            DeadLetterCleanupHostedService.MaxSweepInterval, TimeProvider.System, canceled).IsCanceled);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            _ = Task.Delay(
                DeadLetterCleanupHostedService.MaxSweepInterval + TimeSpan.FromMilliseconds(1),
                TimeProvider.System,
                canceled);
        });
    }

    public static TheoryData<TimeSpan> OutOfRangeIntervals()
    {
        var intervals = new TheoryData<TimeSpan>();
        intervals.Add(TimeSpan.Zero);
        intervals.Add(TimeSpan.FromHours(-1));
        intervals.Add(DeadLetterCleanupHostedService.MaxSweepInterval + TimeSpan.FromMilliseconds(1));
        // A bare number in configuration binds as days, so "720" meant as hours lands here
        intervals.Add(TimeSpan.Parse("720", CultureInfo.InvariantCulture));
        return intervals;
    }

    [Theory]
    [MemberData(nameof(OutOfRangeIntervals))]
    public void SweepInterval_ConfiguredOutOfRange_FallsBackToDefault(TimeSpan configured)
    {
        var globalSettings = new GlobalSettings();
        globalSettings.EventLogging.AzureServiceBus.DeadLetterSweepInterval = configured;

        Assert.Equal(
            GlobalSettings.EventLoggingSettings.AzureServiceBusSettings.DefaultDeadLetterSweepInterval,
            DeadLetterCleanupHostedService.SweepInterval(
                globalSettings,
                NullLogger<DeadLetterCleanupHostedService>.Instance));
    }

    [Fact]
    public void IntegrationSubscriptionNames_CoversEveryConfiguredIntegrationSubscription()
    {
        var globalSettings = new GlobalSettings();
        var settings = globalSettings.EventLogging.AzureServiceBus;

        var configured = typeof(GlobalSettings.EventLoggingSettings.AzureServiceBusSettings)
            .GetProperties()
            .Where(property => property.Name.EndsWith("IntegrationSubscriptionName"))
            .Select(property => (string)property.GetValue(settings)!);

        Assert.Equal(
            configured.OrderBy(name => name),
            DeadLetterCleanupHostedService.IntegrationSubscriptionNames(globalSettings).OrderBy(name => name));
    }

    [Fact]
    public async Task ProcessBatchAsync_MessagesOlderThanCutoff_DeletesThem()
    {
        var older = BuildMessage("older", _cutoff.AddDays(-3));
        var oldest = BuildMessage("oldest", _cutoff.AddDays(-10));
        var receiver = BuildReceiver(oldest, older);

        var result = await DeadLetterCleanupHostedService.ProcessBatchAsync(
            receiver, _cutoff, CancellationToken.None);

        Assert.Equal(2, result.Deleted);
        await receiver.Received(1).CompleteMessageAsync(oldest, Arg.Any<CancellationToken>());
        await receiver.Received(1).CompleteMessageAsync(older, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessBatchAsync_MessageWithinRetention_AbandonsAndStops()
    {
        var withinRetention = BuildMessage("within-retention", _cutoff.AddMinutes(1));
        var receiver = BuildReceiver(withinRetention);

        var result = await DeadLetterCleanupHostedService.ProcessBatchAsync(
            receiver, _cutoff, CancellationToken.None);

        Assert.Equal(0, result.Deleted);
        Assert.False(result.ContinueSweep);
        await receiver.Received(1).AbandonMessageAsync(
            withinRetention,
            Arg.Any<IDictionary<string, object>>(),
            Arg.Any<CancellationToken>());
        await receiver.DidNotReceiveWithAnyArgs().CompleteMessageAsync(default!, default);
    }

    [Fact]
    public async Task ProcessBatchAsync_MessageWithinRetention_LeavesNewerMessagesInBatch()
    {
        var old = BuildMessage("old", _cutoff.AddDays(-1));
        var withinRetention = BuildMessage("within-retention", _cutoff.AddMinutes(1));
        var newer = BuildMessage("newer", _cutoff.AddMinutes(2));
        var receiver = BuildReceiver(old, withinRetention, newer);

        var result = await DeadLetterCleanupHostedService.ProcessBatchAsync(
            receiver, _cutoff, CancellationToken.None);

        Assert.Equal(1, result.Deleted);
        await receiver.Received(1).CompleteMessageAsync(old, Arg.Any<CancellationToken>());
        await receiver.DidNotReceive().CompleteMessageAsync(newer, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessBatchAsync_EmptyQueue_StopsSweep()
    {
        var receiver = BuildReceiver();

        var result = await DeadLetterCleanupHostedService.ProcessBatchAsync(
            receiver, _cutoff, CancellationToken.None);

        Assert.Equal(0, result.Deleted);
        Assert.False(result.ContinueSweep);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(DeadLetterCleanupHostedService.BatchSize)]
    public async Task ProcessBatchAsync_NonEmptyBatchAllDeleted_ContinuesSweep(int messageCount)
    {
        var messages = Enumerable.Range(0, messageCount)
            .Select(i => BuildMessage($"message-{i}", _cutoff.AddDays(-1)))
            .ToArray();
        var receiver = BuildReceiver(messages);

        var result = await DeadLetterCleanupHostedService.ProcessBatchAsync(
            receiver, _cutoff, CancellationToken.None);

        Assert.Equal(messageCount, result.Deleted);
        Assert.True(result.ContinueSweep);
    }
}
