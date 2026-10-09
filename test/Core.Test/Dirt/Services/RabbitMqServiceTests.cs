#nullable enable

using Bit.Core.Dirt.Services.Implementations;
using Bit.Core.Settings;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using Xunit;

namespace Bit.Core.Test.Dirt.Services;

public class RabbitMqServiceTests
{
    private const string _deadLetterQueueName = "test-dead-letter-queue";
    private const string _integrationExchangeName = "test-integration-exchange";
    private const string _deadLetterRoutingKey = "dead-letter";

    private static RabbitMqService BuildSut(TimeSpan deadLetterTimeToLive)
    {
        var globalSettings = new GlobalSettings();
        globalSettings.EventLogging.RabbitMq.DeadLetterTimeToLive = deadLetterTimeToLive;
        globalSettings.EventLogging.RabbitMq.IntegrationDeadLetterQueueName = _deadLetterQueueName;
        globalSettings.EventLogging.RabbitMq.IntegrationExchangeName = _integrationExchangeName;

        return new RabbitMqService(globalSettings, NullLogger<RabbitMqService>.Instance);
    }

    private static OperationInterruptedException BuildInequivalentArgumentFailure() =>
        new(new ShutdownEventArgs(
            ShutdownInitiator.Peer,
            406,
            "PRECONDITION_FAILED - inequivalent arg 'x-message-ttl' for queue"));

    private static IChannel BuildChannel(bool declareFails)
    {
        var channel = Substitute.For<IChannel>();
        if (declareFails)
        {
            channel.QueueDeclareAsync(
                    Arg.Any<string>(),
                    Arg.Any<bool>(),
                    Arg.Any<bool>(),
                    Arg.Any<bool>(),
                    Arg.Any<IDictionary<string, object?>?>(),
                    Arg.Any<bool>(),
                    Arg.Any<bool>(),
                    Arg.Any<CancellationToken>())
                .ThrowsAsync(BuildInequivalentArgumentFailure());
        }

        return channel;
    }

    private static IConnection BuildConnection(IChannel channel)
    {
        var connection = Substitute.For<IConnection>();
        connection.CreateChannelAsync(Arg.Any<CreateChannelOptions?>(), Arg.Any<CancellationToken>())
            .Returns(channel);

        return connection;
    }

    private static async Task AssertDeadLetterQueueBoundAsync(IChannel channel)
    {
        await channel.Received().QueueBindAsync(
            Arg.Is(_deadLetterQueueName),
            Arg.Is(_integrationExchangeName),
            Arg.Is(_deadLetterRoutingKey),
            Arg.Any<IDictionary<string, object?>?>(),
            Arg.Any<bool>(),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(7 * 24)]
    [InlineData(0)]
    public async Task DeclareDeadLetterQueueAsync_ExistingQueueRejectsRedeclare_BindsWithoutThrowing(int timeToLiveHours)
    {
        var channel = BuildChannel(declareFails: true);
        var sut = BuildSut(TimeSpan.FromHours(timeToLiveHours));

        await sut.DeclareDeadLetterQueueAsync(BuildConnection(channel));

        await AssertDeadLetterQueueBoundAsync(channel);
    }

    [Fact]
    public async Task DeclareDeadLetterQueueAsync_RetentionApplies_DeclaresOnceWithMessageTtl()
    {
        var channel = BuildChannel(declareFails: false);
        var sut = BuildSut(TimeSpan.FromDays(7));

        await sut.DeclareDeadLetterQueueAsync(BuildConnection(channel));

        await channel.Received(1).QueueDeclareAsync(
            Arg.Is(_deadLetterQueueName),
            Arg.Is(true),
            Arg.Is(false),
            Arg.Is(false),
            Arg.Is<IDictionary<string, object?>?>(arguments =>
                arguments != null && (int)arguments["x-message-ttl"]! == 604800000),
            Arg.Any<bool>(),
            Arg.Any<bool>(),
            Arg.Any<CancellationToken>());
        await AssertDeadLetterQueueBoundAsync(channel);
    }

    [Fact]
    public void BuildDeadLetterQueueArguments_PositiveTimeToLive_SetsMessageTtlInMilliseconds()
    {
        var arguments = RabbitMqService.BuildDeadLetterQueueArguments(TimeSpan.FromDays(2));

        Assert.NotNull(arguments);
        Assert.Equal(172800000, arguments["x-message-ttl"]);
    }

    [Fact]
    public void BuildDeadLetterQueueArguments_ZeroTimeToLive_ReturnsNoArguments()
    {
        Assert.Null(RabbitMqService.BuildDeadLetterQueueArguments(TimeSpan.Zero));
    }

    [Fact]
    public void BuildDeadLetterQueueArguments_NegativeTimeToLive_ReturnsNoArguments()
    {
        Assert.Null(RabbitMqService.BuildDeadLetterQueueArguments(TimeSpan.FromSeconds(-1)));
    }

    [Fact]
    public void BuildDeadLetterQueueArguments_TimeToLiveBeyondIntRange_CapsAtMaxValue()
    {
        var arguments = RabbitMqService.BuildDeadLetterQueueArguments(TimeSpan.FromDays(365));

        Assert.NotNull(arguments);
        Assert.Equal(int.MaxValue, arguments["x-message-ttl"]);
    }
}
